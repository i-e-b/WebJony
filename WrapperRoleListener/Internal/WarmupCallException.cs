using System;

namespace WrapperRoleListener.Internal
{
    internal class WarmupCallException : Exception
    {
        public WarmupCallException(string message):base(message) { }
    }
}