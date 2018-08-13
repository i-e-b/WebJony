using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using WrapperRoleListener.Internal.Security;

namespace JwtEndpointCaller
{
    /// <summary>
    /// HTTP Client that includes Azure Active Directory JWT headers
    /// </summary>
    public class SecuredHttpClient
    {
        // Security tennant key. Must be the same across the AAD organisation.
        private static readonly string TennantKey = SecuritySettings.Config.TennantKey;
        
        // These are required to create a security token:
        private static readonly string AadAuthority = SecuritySettings.Config.AadAuthorityRoot + TennantKey;
        private static readonly string ClientId = SecuritySettings.Config.ClientId;
        private static readonly string AppKey = SecuritySettings.Config.AppKey;
        private static readonly string ResourceId = SecuritySettings.Config.ResourceId;

        private readonly HttpClient client = ClientWithDefaultProxy();

        /// <summary>
        /// Timeout for making HTTP calls. Defaults to 500ms
        /// </summary>
        public TimeSpan ServiceRequestTimeout { get; set; }


        /// <summary>
        /// Get a url, without secured headers
        /// </summary>
        public HttpResponseMessage GetUnsecuredSync(string url, int? apiVersion)
        {
            return Sync.Run(() => SecureRequestAsync(null, Guid.NewGuid().ToString(), new Uri(url, UriKind.RelativeOrAbsolute), apiVersion, HttpMethod.Get, null, false));
        }

        /// <summary>
        /// Get a url
        /// </summary>
        public HttpResponseMessage GetSync(string url, int? apiVersion, bool showDiag = true)
        {
            return Sync.Run(() => SecureRequestAsync(null, Guid.NewGuid().ToString(), new Uri(url, UriKind.RelativeOrAbsolute), apiVersion, HttpMethod.Get, null, true, showDiag));
        }

        /// <summary>
        /// Post to a url
        /// </summary>
        public HttpResponseMessage PostSync(string url, byte[] data, int? apiVersion)
        {
            return Sync.Run(() => SecureRequestAsync(null, Guid.NewGuid().ToString(), new Uri(url, UriKind.RelativeOrAbsolute), apiVersion, HttpMethod.Post, new ByteArrayContent(data)));
        }

        /// <summary>
        /// General HTTP call, with security headers
        /// </summary>
        /// <param name="sessionId">If given, sets an x-session-id header</param>
        /// <param name="correlationId">If given, sets an x-correlation-id header</param>
        /// <param name="uri">URI to send request to</param>
        /// <param name="apiVersion">version header to send, if any</param>
        /// <param name="httpMethod">HTTP method of the request</param>
        /// <param name="content">Default: empty; content to send with request</param>
        /// <param name="useSecurityHeaders">Default: true; If true, JWT security token will be sent with request. If false, no security token will be sent.</param>
        /// <param name="showDiag">Default: true; Show diagnostic output on the console</param>
        /// <returns>Remote server's response</returns>
        public async Task<HttpResponseMessage> SecureRequestAsync(string sessionId, string correlationId,
            Uri uri, int? apiVersion, HttpMethod httpMethod, HttpContent content = null, bool useSecurityHeaders = true, bool showDiag = true)
        {
            using (var request = new HttpRequestMessage())
            {
                request.RequestUri = uri;
                request.Method = httpMethod;

                //Authentication
                if (useSecurityHeaders)
                {
                    var header = await AuthenticationHeaderValue();
                    request.Headers.Authorization = header;
                }

                if (apiVersion.HasValue) {
                    if (showDiag) Console.WriteLine(apiVersion.Value.ToString());
                    request.Headers.Add("Version", apiVersion.Value.ToString());
                }

                if (!string.IsNullOrWhiteSpace(correlationId)) request.Headers.Add("x-correlation-id", correlationId);
                if (!string.IsNullOrWhiteSpace(sessionId)) request.Headers.Add("x-session-id", sessionId);

                request.Content = content;

                if (showDiag) Console.Write("!");
                return await client.SendAsync(request, CancellationToken.None);
            }
        }

        public static async Task<AuthenticationHeaderValue> AuthenticationHeaderValue()
        {
            var authContext = new AuthenticationContext(AadAuthority);
            var clientCredential = new ClientCredential(ClientId, AppKey);
            var tokens = await authContext.AcquireTokenAsync(ResourceId, clientCredential);
            var header = new AuthenticationHeaderValue(tokens.AccessTokenType, tokens.AccessToken);
            return header;
        }

        /// <summary>
        /// Create a HttpClient with system default proxy
        /// </summary>
        private static HttpClient ClientWithDefaultProxy()
        {
            var timeout = 600000;

            var defaultProxy = WebRequest.DefaultWebProxy;
            if (defaultProxy == null)
            {
                return new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeout) }; // no proxy on this system
            }

            defaultProxy.Credentials = CredentialCache.DefaultCredentials;
            var httpClientHandler = new HttpClientHandler
            {
                Proxy = defaultProxy,
                UseProxy = true
            };

            return new HttpClient(httpClientHandler) { Timeout = TimeSpan.FromMilliseconds(timeout) };
        }
    }
}