using System.Diagnostics;
using System.Threading;

namespace WrapperRoleListener.Internal
{
    /// <summary>
    /// Trace listener that keeps a limited circular buffer of recent messages
    /// </summary>
    internal class LocalTrace: TraceListener
    {
        static LocalTrace()
        {
            Instance = new LocalTrace();
        }

        public static TraceListener Instance { get; set; }

        public static string ReadAll(){
            return ((LocalTrace)Instance).Read();
        }

        private readonly CircularString _buffer;

        public LocalTrace()
        {
            _buffer = new CircularString(8172);
        }

        public override void Write(string message)
        {
            ThreadPool.QueueUserWorkItem(_buffer.Write, message);
        }

        public override void WriteLine(string message)
        {
            ThreadPool.QueueUserWorkItem(_buffer.WriteLine, message);
        }

        public string Read(){
            return _buffer.ReadAll();
        }
    }
}