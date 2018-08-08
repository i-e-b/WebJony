using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Huygens.Compatibility;

namespace WrapperRoleListener.Internal
{
    public class IsapiRequest : IRequest
    {
        private readonly IntPtr _conn;
        private readonly string _pathInfo;
        private readonly Delegates.GetServerVariableDelegate _getServerVariable;

        public IsapiRequest(IntPtr conn, string verb, string query, string pathInfo, string pathTranslated, string contentType, int bytesDeclared, int bytesAvailable, IntPtr data, Delegates.GetServerVariableDelegate getServerVariable, Delegates.WriteClientDelegate writeClient, Delegates.ReadClientDelegate readClient, IntPtr serverSupport)
        {
            _conn = conn;
            _pathInfo = pathInfo;
            _getServerVariable = getServerVariable;

            HttpMethod = verb;

            // TODO: make these lazy?

            if (bytesDeclared > 0) InputStream = new IsapiClientStream(_conn, bytesAvailable, bytesDeclared, data, readClient);
            else InputStream = new MemoryStream();

            QueryString = query.ParseQueryString();
            Headers = new DictionaryHeaderWrapper(SplitToDict(GetServerUnicodeVariable("UNICODE_ALL_RAW")));
            IsSecureConnection = GetServerAnsiVariable("HTTPS") == "ON";

            RawUrl = GetFullRawUrl();

            ProtocolVersion = ParseVersion(GetServerAnsiVariable("SERVER_PROTOCOL"));
            HasAcceptEncoding = Headers.Get("Accept-Encoding") != null;
            
            RemoteEndPoint = new IPEndPoint(IPAddress.Parse(GetServerAnsiVariable("REMOTE_ADDR")), 0);

            ContentEncoding = GuessContentEncoding();
        }

        private string GetFullRawUrl()
        {
            var pathAndQuery = GetServerUnicodeVariable("UNICODE_HTTP_URL");
            var scheme = IsSecureConnection ? "https://" : "http://";
            var host = GetServerUnicodeVariable("UNICODE_SERVER_NAME");
            var port = GetServerAnsiVariable("SERVER_PORT");

            return scheme + host + ":" + port + pathAndQuery;
        }

        private Encoding GuessContentEncoding()
        {
            var header = Headers["Content-Type"];
            if (header == null || ! header.Contains("charset=")) return Encoding.UTF8;

            int idx = header.IndexOf("charset=", StringComparison.Ordinal);
            try {
                return Encoding.GetEncoding(header.Substring(idx + 8));
            } catch {
                return Encoding.UTF8;
            }
        }

        private Version ParseVersion(string protocolString)
        {
            if (protocolString.StartsWith("HTTP/"))
                return Version.Parse(protocolString.Substring(5));
            return new Version(1,1); // just guess
        }

        public NameValueCollection QueryString { get; }
        public Uri Url { get { return new Uri(RawUrl, UriKind.RelativeOrAbsolute); } }
        public Stream InputStream { get; }
        public Encoding ContentEncoding { get; }
        public IPEndPoint RemoteEndPoint { get; }
        public bool HasAcceptEncoding { get; }
        public Version ProtocolVersion { get; }
        public string HttpMethod { get; }
        public string RawUrl { get; }
        public IHeaderCollection Headers { get; }
        public bool IsSecureConnection { get; }

        public IPAddress EndpointAddress()
        {
            var raw = GetServerAnsiVariable("REMOTE_ADDR");
            return IPAddress.Parse(raw);
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
    }
}