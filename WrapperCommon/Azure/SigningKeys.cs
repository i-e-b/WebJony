using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using SkinnyJson;
using WrapperCommon.Security;

namespace WrapperCommon.Azure
{
    public static class SigningKeys
    {
        /// <summary>
        /// Map of kid=>x5c data
        /// </summary>
        private static readonly Dictionary<string,string> KeyCache = new Dictionary<string,string>();
        private static readonly object KeyLock = new object();

        /// <summary>
        /// Return a disposable collection of security tokens for all known signing keys.
        /// <para>Caller must dispose</para>
        /// </summary>
        public static DisposingContainer<X509SecurityToken> AllAvailableKeys()
        {
            var collection = new DisposingContainer<X509SecurityToken>();
            lock (KeyLock)
            {
                foreach (var key in KeyCache.Keys)
                {
                    collection.Add(new X509SecurityToken(PublicKeyForKid(key)));
                }
            }
            return collection;
        }

        /// <summary>
        /// Look up the Public Key for a given KID.
        /// KID is case sensitive. Returns `null` if no public key is found.
        /// <para>A new certificate is created for each call, and should be disposed by the caller</para>
        /// </summary>
        public static X509Certificate2 PublicKeyForKid(string kid) {
            lock (KeyLock)
            {
                if (!KeyCache.ContainsKey(kid)) return null;
                return new X509Certificate2(Convert.FromBase64String(KeyCache[kid]));
            }
        }

        /// <summary>
        /// Update the key cache on a background thread. This method will return before the new keys are available.
        /// </summary>
        public static void UpdateKeyCache()
        {
            var keyDiscoveryUrl = SecuritySettings.Config.KeyDiscoveryUrl;
            if (keyDiscoveryUrl == null) return;

            new Thread(()=>{
                using (var client = ClientWithDefaultProxy()) {
                    // ReSharper disable once AccessToDisposedClosure
                    var str = Sync.Run(() => client.GetStringAsync(keyDiscoveryUrl));
                    var data = Json.Defrost<JwkSet>(str);

                    foreach (var key in data.keys)
                    {
                        if (key.x5c.Count != 1) continue; // we don't handle multi-part certificates
                        if (key.use != "sig") continue;   // we only use signature keys
                        if (key.kty != "RSA") continue;   // currently only uses RSA certificates

                        lock (KeyLock)
                        {
                            if (KeyCache.ContainsKey(key.kid)) KeyCache[key.kid] = key.x5c[0];
                            else KeyCache.Add(key.kid, key.x5c[0]);
                        }
                    }

                }
            }).Start();
        }

        /// <summary>
        /// Create a HttpClient with system default proxy
        /// </summary>
        private static HttpClient ClientWithDefaultProxy()
        {
            var defaultProxy = WebRequest.DefaultWebProxy;
            if (defaultProxy == null)
            {
                return new HttpClient { Timeout = TimeSpan.FromSeconds(5) }; // no proxy on this system
            }

            defaultProxy.Credentials = CredentialCache.DefaultCredentials;
            var httpClientHandler = new HttpClientHandler
            {
                Proxy = defaultProxy,
                PreAuthenticate = true,
                UseDefaultCredentials = true
            };

            return new HttpClient(httpClientHandler) { Timeout = TimeSpan.FromSeconds(5) };
        }

    }

    /// <summary>
    /// https://tools.ietf.org/html/rfc7517
    /// </summary>
    public class JwkSet
    {
        public List<JsonWebKey> keys { get; set; }
    }

    /// <summary>
    /// https://tools.ietf.org/html/rfc7517#section-4
    /// </summary>
    public class JsonWebKey
    {
        /// <summary> Key algorithm type </summary>
        public string kty { get; set; }

        /// <summary> Key purpose </summary>
        public string use { get; set; }

        /// <summary> Key identifier </summary>
        public string kid { get; set; }

        /// <summary> X509 Thumbprint </summary>
        public string x5t { get; set; }
        
        /// <summary> RSA modulus </summary>
        public string n { get; set; }
        
        /// <summary> RSA exponent </summary>
        public string e { get; set; }
        
        /// <summary> X509 certificates </summary>
        public List<string> x5c { get; set; }
    }
}