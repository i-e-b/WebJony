namespace WrapperRoleListener.Core
{
    /// <summary>
    /// Named listening point for HTTP(S)
    /// </summary>
    public class Endpoint
    {
        public string Name { get; set; }
        public string Protocol { get; set; }
        public string IPEndpoint { get; set; }
    }
}