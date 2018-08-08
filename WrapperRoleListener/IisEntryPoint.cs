using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Text;
using Huygens;
using Tag;
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
            BaseDirectory = basePath;
            output = null;

            // Do the wake up, similar to the CoreListener class
            try
            {
                // Start re-populating signing keys. If the code-cached keys are out of date, it may take a few seconds to freshen.
                SigningKeys.UpdateKeyCache();

                // Set up the internal trace
                Trace.UseGlobalLock = false;
                Trace.AutoFlush = false;
                Trace.Listeners.Add(LocalTrace.Instance);

                // Load the config file
                var configurationMap = new ExeConfigurationFileMap { ExeConfigFilename = BaseDirectory + ".config" };
                WrapperRequestHandler.ExplicitConfiguration = ConfigurationManager.OpenMappedExeConfiguration(configurationMap, ConfigurationUserLevel.None);

                // TODO: find some way of checking if we have a HTTPS endpoint bound?
                WrapperRequestHandler.HttpsAvailable = true;

                // Load the wrapper
                _core = new WrapperRequestHandler(new AadSecurityCheck());
            }
            catch (Exception ex)
            {
                RecPrintException(ex);
                output = BaseDirectory + "\r\n" + ex;
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

        private static void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            var ex = e.Exception;
            if (ex != null) RecPrintException(ex);
            else File.AppendAllText(@"C:\Temp\FirstChanceException.txt", "\r\n\r\nNo exception");
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex) RecPrintException(ex);
            else File.AppendAllText(@"C:\Temp\RootException.txt", "\r\n\r\nNo exception");
        }

        private static void RecPrintException(Exception ex)
        {
            if (ex == null) return;
            File.AppendAllText(@"C:\Temp\RootException.txt", "\r\n\r\n" + ex);
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
                /*

                var headerString = TryGetHeaders(conn, getServerVariable);

                var physicalPath = TryGetPhysPath(conn, getServerVariable);

                var remoteAddr = TryGetRemoteAddr(conn, getServerVariable);
                
                var head = T.g("head")[T.g("title")[".Net Output"]];
                
                var body = T.g("body")[
                    T.g("h1")["Hello"],
                    T.g("p")[".Net here!"],
                    T.g("p")["Core was ", (_core == null ? "null" : "ok")],
                    T.g("p")["You called me with these properties:"],

                    T.g("dl")[
                        Def("Verb", verb),
                        Def("Query string", query),
                        Def("URL path", pathInfo),
                        Def("Equivalent file path", pathTranslated),
                        Def("App physical path", physicalPath),
                        Def("Remote address", remoteAddr),
                        Def("Requested content type", contentType)
                    ],

                    T.g("p")["Request headers: ",
                        T.g("pre")[headerString]
                    ],

                    T.g("p")["Client supplied " + bytesAvailable + " bytes out of an expected " + bytesDeclared + " bytes"]
                ];

                var rq = new SerialisableRequest
                {
                    Headers = SplitToDict(headerString),
                    Method = verb,
                    RequestUri = pathInfo,
                    Content = ReadAllContent(conn, bytesAvailable, bytesDeclared, data, readClient)
                };
                if (!string.IsNullOrWhiteSpace(query)) rq.RequestUri += "?" + query;

                var page = T.g("html")[ head, body ];

                var ms = new MemoryStream();
                page.StreamTo(ms, Encoding.UTF8);
                ms.WriteByte(0);
                ms.Seek(0, SeekOrigin.Begin);
                var msg = ms.ToArray();
                int len = msg.Length;

                TryWriteHeaders(conn, serverSupport);
                writeClient(conn, msg, ref len, 0);
                */
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

                TryWriteHeaders(conn, serverSupport);
                writeClient(conn, msg, ref len, 0);
            }
        }
        
        [SuppressUnmanagedCodeSecurity]
        private static byte[] ReadAllContent(IntPtr connId, int bytesAvailable, int bytesDeclared, IntPtr data, Delegates.ReadClientDelegate readClient)
        {
            if (bytesDeclared < 1) return null;
            
            var dataStream = new IsapiClientStream(connId, bytesAvailable, bytesDeclared, data, readClient);
            var msin = new MemoryStream();
            dataStream.CopyTo(msin);
            msin.Seek(0, SeekOrigin.Begin);
            return msin.ToArray();
        }

        private static Dictionary<string, string> SplitToDict(string headerString)
        {
            var output = new Dictionary<string,string>();
            var lines = headerString.Split(new []{'\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var bits = line.Split(new []{ ": "}, 2, StringSplitOptions.None);
                if (bits.Length != 2) continue; // invalid header

                var key = bits[0].Trim();
                var value = bits[1].Trim();
                if (output.ContainsKey(key)) output[key] = output[key] + ", " + value;
                else output.Add(key, value);
            }
            return output;
        }

        private static TagContent Def(string name, string value)
        {
            return T.g()[
                T.g("dt")[name],
                T.g("dd")[value]
            ];
        }

        /// <summary>
        /// Read headers from the incoming request
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        private static string TryGetHeaders(IntPtr conn, Delegates.GetServerVariableDelegate callback)
        {
            var size = 4096;
            var buffer = Marshal.AllocHGlobal(size);
            try {
                callback(conn, "UNICODE_ALL_RAW", buffer, ref size);
                return Marshal.PtrToStringUni(buffer); // 'Uni' here matches 'UNICODE_' above.
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [SuppressUnmanagedCodeSecurity]
        private static string TryGetRemoteAddr(IntPtr conn, Delegates.GetServerVariableDelegate callback)
        {
            var size = 4096;
            var buffer = Marshal.AllocHGlobal(size);
            try {
                callback(conn, "REMOTE_ADDR", buffer, ref size);
                return Marshal.PtrToStringAnsi(buffer);
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
        }
        
        [SuppressUnmanagedCodeSecurity]
        private static string TryGetPhysPath(IntPtr conn, Delegates.GetServerVariableDelegate callback)
        {
            var size = 4096;
            var buffer = Marshal.AllocHGlobal(size);
            try {
                callback(conn, "APPL_PHYSICAL_PATH", buffer, ref size);
                return Marshal.PtrToStringAnsi(buffer); // Must be ANSI for this variable
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Test of 'ex' status writing
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        private static void TryWriteHeaders(IntPtr conn, IntPtr ss)
        {
            var headerCall = Marshal.GetDelegateForFunctionPointer<Delegates.ServerSupportFunctionDelegate_Headers>(ss);

            var data = new SendHeaderExInfo
            {
                fKeepConn = false,
                pszHeader = "X-Fish: I come from the marshall\r\nX-CB: "+typeof(DirectServer).Assembly.CodeBase+"\r\nContent-type: text/html\r\n\r\n",
                pszStatus = "200 OK-dokey"
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