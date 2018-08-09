using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace WrapperRoleListener.Internal
{
    /// <summary>
    /// Trace listener that keeps a limited circular buffer of recent messages
    /// </summary>
    internal class LocalTrace: TraceListener
    {
        public static TraceListener Instance { get; set; }
        private readonly Queue<string> q;
        private readonly object _lock = new object();
        private readonly CircularString _buffer;
        
        private const int LogLimit = 2048; // Start ignoring logs if this many back up.

        static LocalTrace()
        {
            Instance = new LocalTrace();
        }

        public static string ReadAll(){
            return ((LocalTrace)Instance).Read();
        }


        public LocalTrace()
        {
            _buffer = new CircularString(8172);
            q = new Queue<string>();
            var thread = new Thread(()=>{
                    while (true)
                    {
                        if (q.Count < 1) Thread.Sleep(5000);
                        lock (_lock)
                        {
                            while (q.Count > 0)
                            {
                                var s = q.Dequeue();
                                _buffer.Write(s);
                            }
                        }
                    }
                    // ReSharper disable once FunctionNeverReturns
                })
                {IsBackground = true, Priority = ThreadPriority.Lowest}; // We will only write to the console while idle
            thread.Start();
        }

        public override void Write(string message)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (q.Count > LogLimit) return;
            lock(_lock){
                q.Enqueue(message);
            }
        }

        public override void WriteLine(string message)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (q.Count > LogLimit) return;
            lock(_lock){
                q.Enqueue(message + "\r\n");
            }
        }

        public string Read(){
            return _buffer.ReadAll();
        }
    }
}