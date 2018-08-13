using System;

namespace WrapperRoleListener.Internal
{
    /// <summary>
    /// An exception used to signal file-access conflicts during warm-up and scanning
    /// </summary>
    internal class WarmupCallException : Exception
    {
        public WarmupCallException(string message):base(message) { }
    }
}