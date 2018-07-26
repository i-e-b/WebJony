using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace WrapperRoleListener.Internal
{
    public class ConsoleTrace: TraceListener
    {
        public static readonly ConsoleTrace Instance = new ConsoleTrace();
        private readonly Queue<string> q;
        private readonly object _lock = new object();

        private const int LogLimit = 2048; // Start ignoring logs if this many back up.

        public ConsoleTrace()
        {
            q = new Queue<string>();
            var thread = new Thread(()=>{
                var outs = Console.OpenStandardOutput();
                var b = new byte[4096]; // limit of log line length

                while (true)
                {
                    if (q.Count < 1) Thread.Sleep(5000);
                    lock (_lock)
                    {
                        while (q.Count > 0)
                        {
                            var s = q.Dequeue();
                            var len = Encoding.ASCII.GetBytes(s, 0, s.Length, b, 0);
                            outs.Write(b, 0, len);
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
                q.Enqueue(message);
                q.Enqueue("\r\n");
            }
        }
    }
}