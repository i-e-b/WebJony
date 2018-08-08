using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Web.SessionState;
using Huygens;
using Huygens.Compatibility;

namespace WrapperRoleListener.Internal
{
    /// <summary>
    /// Huygens IContext wrapper for a single Isapi request/response
    /// </summary>
    public class IsapiContext : IContext
    {
        private readonly IntPtr _conn;
        private readonly string _verb;
        private readonly string _pathInfo;
        private readonly Delegates.GetServerVariableDelegate _getServerVariable;

        public IsapiContext(
            IntPtr conn,
            [MarshalAs(UnmanagedType.LPStr)] string verb,
            [MarshalAs(UnmanagedType.LPStr)] string query,
            [MarshalAs(UnmanagedType.LPStr)] string pathInfo,
            [MarshalAs(UnmanagedType.LPStr)] string pathTranslated,
            [MarshalAs(UnmanagedType.LPStr)] string contentType,

            Int32 bytesDeclared,
            Int32 bytesAvailable, // if available < declared, you need to run `readClient` to get more
            IntPtr data, // first blush of data, if any

            Delegates.GetServerVariableDelegate getServerVariable, Delegates.WriteClientDelegate writeClient, Delegates.ReadClientDelegate readClient, IntPtr serverSupport)
        {
            _conn = conn;
            _verb = verb;
            _pathInfo = pathInfo;
            _getServerVariable = getServerVariable;

            Request = new IsapiRequest(conn, verb, query, pathInfo, pathTranslated, contentType,
                bytesDeclared, bytesAvailable, data, getServerVariable,
                writeClient, readClient, serverSupport);
            
            Response = new IsapiResponse(conn, verb, query, pathInfo, pathTranslated, contentType,
                bytesDeclared, bytesAvailable, data, getServerVariable,
                writeClient, readClient, serverSupport);
        }

        public IPAddress EndpointAddress()
        {
            var raw = GetServerAnsiVariable("REMOTE_ADDR");
            return IPAddress.Parse(raw);
        }

        public string Verb()
        {
            return _verb;
        }

        public string Path()
        {
            return _pathInfo;
        }

        public string Extension()
        {
            return System.IO.Path.GetExtension(_pathInfo);
        }

        public string MapPath(string relativePath)
        {
            return System.IO.Path.Combine(GetServerAnsiVariable("APPL_PHYSICAL_PATH"), relativePath);
        }

        public void Redirect(string url)
        {
            Response.Redirect(url);
        }

        public IRequest Request { get; }
        public IResponse Response { get; }

        /// <summary>
        /// Not supported
        /// </summary>
        public HttpSessionState Session => null;

        public bool IsLocal
        {
            get
            {
                var ip = GetServerAnsiVariable("REMOTE_ADDR");
                return ip == "::1" || ip == "127.0.0.1";
            }
        }

        public bool IsSecureConnection
        {
            get
            {
                return GetServerAnsiVariable("HTTPS") == "ON";
            }
        }




        [SuppressUnmanagedCodeSecurity]
        private byte[] ReadAllContent(IntPtr connId, int bytesAvailable, int bytesDeclared, IntPtr data, Delegates.ReadClientDelegate readClient)
        {
            if (bytesDeclared < 1) return null;
            
            var dataStream = new IsapiClientStream(connId, bytesAvailable, bytesDeclared, data, readClient);
            var msin = new MemoryStream();
            dataStream.CopyTo(msin);
            msin.Seek(0, SeekOrigin.Begin);
            return msin.ToArray();
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
        private string GetServerUnicodeVariable(string variableName)
        {
            var size = 4096;
            var buffer = Marshal.AllocHGlobal(size);
            try {
                _getServerVariable(_conn, variableName, buffer, ref size);
                return Marshal.PtrToStringUni(buffer);
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
        }
        
        [SuppressUnmanagedCodeSecurity]
        private string GetServerAnsiVariable(string variableName)
        {
            var size = 4096;
            var buffer = Marshal.AllocHGlobal(size);
            try {
                _getServerVariable(_conn, variableName, buffer, ref size);
                return Marshal.PtrToStringAnsi(buffer);
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}