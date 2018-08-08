using System;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Huygens;
using Huygens.Compatibility;

namespace WrapperRoleListener.Internal
{
    public class IsapiResponse : IResponse
    {
        private readonly IntPtr _conn;
        private readonly Delegates.WriteClientDelegate _writeClient;
        private readonly IntPtr _serverSupport;

        public IsapiResponse(IntPtr conn, string verb, string query, string pathInfo, string pathTranslated, string contentType, int bytesDeclared, int bytesAvailable, IntPtr data, Delegates.GetServerVariableDelegate getServerVariable, Delegates.WriteClientDelegate writeClient, Delegates.ReadClientDelegate readClient, IntPtr serverSupport)
        {
            _conn = conn;
            _writeClient = writeClient;
            _serverSupport = serverSupport;
            OutputStream = new MemoryStream();
            Headers = new NameValueCollectionHeaderWrapper(new NameValueCollection());
        }

        public void Close()
        {
            int len;
            var ms = (MemoryStream)OutputStream;
            ms.WriteByte(0);
            ms.Seek(0, SeekOrigin.Begin);
            var msg = ms.ToArray();
            var msgLen =  msg.Length - 1; // don't count null terminator

            TryWriteHeaders(msgLen);
            
            // this chunked stuff is needed if the host-proxy requests chunked, but the underlying client-app doesn't supply it
            // TODO: refine
            if (IsChunked()) {
                // write chunk preamble
                var chunkTerm = Encoding.ASCII.GetBytes(msgLen.ToString("X") + "\r\n\0");
                len = chunkTerm.Length - 1;
                _writeClient(_conn, chunkTerm, ref len, 0);
            }

            len = msgLen;
            _writeClient(_conn, msg, ref len, 0);

            if (IsChunked()) {
                // write chunk terminator
                var chunkTerm = Encoding.ASCII.GetBytes("\r\n0\r\n\r\n\0");
                len = chunkTerm.Length - 1;
                _writeClient(_conn, chunkTerm, ref len, 0);
            }
        }

        public void Write(string data, string contentType = "text/plain", int statusCode = 200)
        {
            Write(Encoding.UTF8.GetBytes(data), contentType, statusCode);
        }

        public void WriteCompressed(string data, string contentType = "text/plain", int statusCode = 200)
        {
            Write(Encoding.UTF8.GetBytes(data), contentType, statusCode); // todo: actual compression
        }

        public void Write(byte[] data, string contentType = "text/plain", int statusCode = 200)
        {
            OutputStream.Write(data, 0, data.Length);
            ContentType = contentType;
            StatusCode = statusCode;
        }

        public void Redirect(string newTarget)
        {
            StatusCode = 302;
            Headers.Set("Location", newTarget);
        }

        public int StatusCode { get; set; }
        public string ContentType { get; set; }
        public Encoding ContentEncoding { get; set; }
        public long ContentLength64 { get; set; }
        public Stream OutputStream { get; }
        public IHeaderCollection Headers { get; set; }
        public bool SendChunked
        {
            get { return IsChunked(); }
            set { SetChunked(value); }
        }

        public string StatusDescription { get; set; }


        [SuppressUnmanagedCodeSecurity]
        private void TryWriteHeaders(int contentLength)
        {
            var headerCall = Marshal.GetDelegateForFunctionPointer<Delegates.ServerSupportFunctionDelegate_Headers>(_serverSupport);
             
            var sb = new StringBuilder();
            foreach (var key in Headers.AllKeys) {
                sb.Append(key);
                sb.Append(": ");
                sb.Append(Headers[key]);
                sb.Append("\r\n");
            }
            if (Headers.Get("Content-Type") == null) {
                var ctype = ContentType ?? "text/plain";
                sb.Append("Content-type: ");
                sb.Append(ctype);
                sb.Append("\r\n");
            }
            if (Headers.Get("Content-Length") == null && ! IsChunked()) {
                sb.Append("Content-Length: ");
                sb.Append(contentLength.ToString());
                sb.Append("\r\n");
            }
            sb.Append("\r\n");

            var data = new SendHeaderExInfo
            {
                fKeepConn = false,
                pszHeader = sb.ToString(),
                pszStatus = StatusCode + " " + StatusDescription
            };

            data.cchStatus = data.pszStatus.Length;
            data.cchHeader = data.pszHeader.Length;

            headerCall(_conn, Win32.HSE_REQ_SEND_RESPONSE_HEADER_EX, data, IntPtr.Zero, IntPtr.Zero);
        }


        private void SetChunked(bool value)
        {
            var te = Headers.Get("Transfer-Encoding");
            if (te == null)
            {
                if (value == false) return;
                Headers.Set("Transfer-Encoding", "chunked");
                return;
            }

            if (te.Contains("chunked")) {
                if (value) return;
                Headers.Set("Transfer-Encoding", te.Replace("chunked",""));
            } else {
                if (value == false) return;
                Headers.Add("Transfer-Encoding", "chunked");
            }
        }

        private bool IsChunked()
        {
            var te = Headers.Get("Transfer-Encoding");
            if (te == null) return false;
            return te.Contains("chunked");
        }
    }
}