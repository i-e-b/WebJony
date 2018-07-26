using System;

namespace WrapperCommon.AssemblyLoading
{
    /// <summary>
    /// An exception indicating that file scans should be retried later
    /// </summary>
    public class DelayRescanException : Exception
    {
    }
}