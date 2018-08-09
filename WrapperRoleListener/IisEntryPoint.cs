using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using Huygens;
using Microsoft.Web.Administration;
using WrapperCommon.Azure;
using WrapperCommon.Security;
using WrapperRoleListener.Core;
using WrapperRoleListener.Internal;

namespace WrapperRoleListener
{
    /// <summary>
    /// Binds an ISAPI handler for the C++ IIS shim,
    /// passing requests to a WrapperRequestHandler instance
    ///
    /// The C++ side assumes:
    ///     - The C# dll/exe will be in the same directory
    ///     - There is a type `WrapperRoleListener.IisEntryPoint`
    ///     - That type has a public static method `int FindFunctionPointer(string requestType)`
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    [PermissionSet(SecurityAction.Demand, Unrestricted = true)]
    [SecurityPermission(SecurityAction.Demand, Unrestricted = true)]
    [SecurityCritical]
    public class IisEntryPoint
    {
        static GCHandle GcShutdownDelegateHandle;
        static readonly Delegates.VoidDelegate ShutdownPtr;

        static GCHandle GcWakeupDelegateHandle;
        static readonly Delegates.StringStringDelegate WakeupPtr;

        static GCHandle GcHandleRequestDelegateHandle;
        static readonly Delegates.HandleHttpRequestDelegate HandlePtr;

        
        /// <summary>
        /// Allocated directory for config and setup
        /// </summary>
        private static string BaseDirectory;
        private static WrapperRequestHandler _core;

        /// <summary>
        /// Static constructor. Build the function pointers
        /// </summary>
        static IisEntryPoint()
        {
            HardReference<Microsoft.WindowsAzure.Diagnostics.DiagnosticMonitorTraceListener>();

            // Try never to let an exception escape
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException; // you can add this to get insight into the hosted sites

            ShutdownPtr = ShutdownCallback; // get permanent function pointer
            GcShutdownDelegateHandle = GCHandle.Alloc(ShutdownPtr); // prevent garbage collection

            WakeupPtr = WakeupCallback;
            GcWakeupDelegateHandle = GCHandle.Alloc(WakeupPtr);

            HandlePtr = HandleHttpRequestCallback;
            GcHandleRequestDelegateHandle = GCHandle.Alloc(HandlePtr);
        }

        /// <summary>
        /// Handle setup from the C++ side
        /// </summary>
        /// <param name="basePath">base path for the .Net binary</param>
        /// <param name="output">error message, if any</param>
        private static void WakeupCallback(string basePath, out string output)
        {
            BaseDirectory = Path.GetDirectoryName(basePath);
            output = null;

            // Do the wake up, similar to the CoreListener class
            try
            {
                // Start re-populating signing keys. If the code-cached keys are out of date, it may take a few seconds to freshen.
                SigningKeys.UpdateKeyCache();

                // Set up the internal trace
                Trace.UseGlobalLock = false;
                Trace.AutoFlush = false;
                Trace.Listeners.Clear();
                Trace.Listeners.Add(LocalTrace.Instance);

                
                ThreadPool.SetMaxThreads(CoreListener.Parallelism, CoreListener.Parallelism);
                ThreadPool.SetMinThreads(1, 1);

                // Load the config file
                var configurationMap = new ExeConfigurationFileMap { ExeConfigFilename = basePath + ".config" }; // this will load the app.config file.
                WrapperRequestHandler.ExplicitConfiguration = ConfigurationManager.OpenMappedExeConfiguration(configurationMap, ConfigurationUserLevel.None);

                // Check to see if HTTPS is bound in IIS
                if (GetBindings(BaseDirectory).Contains("https")) WrapperRequestHandler.HttpsAvailable = true;

                // Load the wrapper
                _core = new WrapperRequestHandler(new AadSecurityCheck());
            }
            catch (Exception ex)
            {
                RecPrintException(ex);
                output = basePath + "\r\n" + ex;
            }
        }

        /// <summary>
        /// This is a special format of method that can be directly called by `ExecuteInDefaultAppDomain`
        /// <para>See https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/hosting/iclrruntimehost-executeindefaultappdomain-method.</para>
        /// We use this to provide function pointers for the C++ code to use.
        /// </summary>
        public static int FindFunctionPointer(string requestType)
        {
            switch (requestType)
            {
                case "Shutdown":
                    return WriteFunctionPointer(ShutdownPtr);

                case "Handle":
                    return WriteFunctionPointer(HandlePtr);

                case "Wakeup":
                    return WriteFunctionPointer(WakeupPtr);

                default: return -1;
            }
        }


        public static void ShutdownCallback()
        {
            _core?.ShutdownAll();
            GcWakeupDelegateHandle.Free();
            GcShutdownDelegateHandle.Free();
            GcHandleRequestDelegateHandle.Free();
        }

        /// <summary>
        /// A simple demo responder
        /// </summary>
        [PermissionSet(SecurityAction.Demand, Unrestricted = true)]
        [SecurityPermission(SecurityAction.Demand, Unrestricted = true, ControlAppDomain = true, UnmanagedCode = true)]
        [SecurityCritical]
        [SuppressUnmanagedCodeSecurity]
        public static void HandleHttpRequestCallback(
        #region params
            IntPtr conn,
            [MarshalAs(UnmanagedType.LPStr)] string verb,
            [MarshalAs(UnmanagedType.LPStr)] string query,
            [MarshalAs(UnmanagedType.LPStr)] string pathInfo,
            [MarshalAs(UnmanagedType.LPStr)] string pathTranslated,
            [MarshalAs(UnmanagedType.LPStr)] string contentType, // not really useful.

            Int32 bytesDeclared,
            Int32 bytesAvailable, // if available < declared, you need to run `readClient` to get more
            IntPtr data, // first blush of data, if any

            Delegates.GetServerVariableDelegate getServerVariable, Delegates.WriteClientDelegate writeClient, Delegates.ReadClientDelegate readClient, IntPtr serverSupport)
        #endregion
        {
            try
            {
                _core?.Handle(
                    new IsapiContext(
                    conn, verb, query, pathInfo, pathTranslated, contentType,
                    bytesDeclared, bytesAvailable, data, getServerVariable,
                    writeClient, readClient, serverSupport)
                );
            }
            catch (Exception ex)
            {
                var ms = new MemoryStream();
                var bytes = Encoding.UTF8.GetBytes("<pre>" + ex + "</pre>");
                ms.Write(bytes, 0, bytes.Length);
                ms.WriteByte(0);
                ms.Seek(0, SeekOrigin.Begin);
                var msg = ms.ToArray();
                int len = msg.Length;

                WriteErrorHeaders(conn, serverSupport);
                writeClient(conn, msg, ref len, 0);
            }
        }

        
        private static void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            var ex = e.Exception;
            if (ex != null) RecPrintException(ex);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex) RecPrintException(ex);
        }

        private static void RecPrintException(Exception ex)
        {
            if (ex == null) return;
            Trace.TraceError(ex.ToString());
            RecPrintException(ex.InnerException);
        }

        private static int WriteFunctionPointer(Delegate del)
        {
            try
            {
                var bSetOk = Win32.SetSharedMem(Marshal.GetFunctionPointerForDelegate(del).ToInt64());
                return bSetOk ? 1 : 0;
            }
            catch
            {
                return -2;
            }
        }


        /// <summary>
        /// Read the http/https bindings for a local IIS site based on the virtual directory physical path
        /// </summary>
        private static List<string> GetBindings(string targetPath)
        {
            var output = new List<string>();

            // Get the sites section from the AppPool.config 
            var sitesSection = WebConfigurationManager.GetSection(null, null, "system.applicationHost/sites");

            foreach (var site in sitesSection.GetCollection())
            {
                foreach (var application in site.GetCollection())
                {
                    foreach (var virtualDirectory in application.GetCollection())
                    {
                        var phys = (string)virtualDirectory["physicalPath"];
                        if (!targetPath.Equals(phys, StringComparison.OrdinalIgnoreCase)) continue;

                        // For each binding see if they are http based and return the port and protocol 
                        foreach (var binding in site.GetCollection("bindings"))
                        {
                            string protocol = (string)binding["protocol"];

                            if (!protocol.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;


                            // Return it if the path matches
                            output.Add(protocol.ToLowerInvariant());
                        }
                    }
                }
            }
            return output;
        }

        [SuppressUnmanagedCodeSecurity]
        private static void WriteErrorHeaders(IntPtr conn, IntPtr ss)
        {
            var headerCall = Marshal.GetDelegateForFunctionPointer<Delegates.ServerSupportFunctionDelegate_Headers>(ss);

            var data = new SendHeaderExInfo
            {
                fKeepConn = false,
                pszHeader = "X-CB: " + typeof(DirectServer).Assembly.CodeBase + "\r\nContent-type: text/html\r\n\r\n",
                pszStatus = "500 Internal Server Error"
            };

            data.cchStatus = data.pszStatus.Length;
            data.cchHeader = data.pszHeader.Length;

            headerCall(conn, Win32.HSE_REQ_SEND_RESPONSE_HEADER_EX, data, IntPtr.Zero, IntPtr.Zero);
        }
        
        /// <summary>
        /// This is here to ensure we have a hard reference to things implicitly required by Azure
        /// </summary>
        private static void HardReference<TRef>()
        {
            Trace.Assert(typeof(TRef).Name != "?", "Never happens");
        }
    }
}