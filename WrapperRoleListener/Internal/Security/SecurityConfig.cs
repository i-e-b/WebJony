namespace WrapperRoleListener.Internal.Security
{
    /// <summary>
    /// Config keys from json settings file
    /// </summary>
    public struct SecurityConfig
    {
        /// <summary>
        /// Security tennant key. Must be the same across the AAD organisation.
        /// </summary>
        public string TennantKey;

        public string AadAuthorityRoot;
        public string ClientId;
        public string AppKey;
        public string ResourceId;

        public string Audience;
        public string KeyDiscoveryUrl;
        public string AadTokenIssuer;
    }
}