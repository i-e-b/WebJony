using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using WrapperRoleListener.Core;
using WrapperRoleListener.Internal;

namespace WrapperRoleListener
{
    public class ExeEntryPoint
    {
        private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
        private static ListenerLoop core;
        private static int ParentProcessId = -1;

        /// <summary>
        /// This is the entry point for the Wrapper run as a standard exe.
        /// It is usable for self hosting, and is also how the IIS integration is done (This gets around problems with integrated mode).
        /// It is *not* used for Azure integration, as process-to-process communication is very poor.
        /// </summary>
        /// <remarks>
        /// The parameters passed are:
        ///     [0] = listen address
        ///     [1] = parent process ID (the child should terminate if the parent becomes unavailable)
        /// </remarks>
        public static int Main(string[] args){
            Trace.UseGlobalLock = false;
            Console.WriteLine("Waking up listener...");
            Trace.Listeners.Add(ConsoleTrace.Instance);
            Trace.TraceInformation("WrapperRoleListener 'run' was requested");

            var listeningPort = 8080;
            if (args.Length > 0 && int.TryParse(args[0], out var port)) listeningPort = port;
            if (args.Length > 1 && int.TryParse(args[1], out var pProc)) ParentProcessId = pProc;

            var endpoints = new List<Endpoint>{
                new Endpoint{
                    Protocol = "http",
                    Name = "defaultLocal",
                    IPEndpoint = "127.0.0.1:"+listeningPort
                }
            };

            try
            {
                core = new ListenerLoop();
                if (ParentProcessId > 0) { core.PeriodicCheck += Core_PeriodicCheck; }
                core.Listen(cancellationTokenSource.Token, endpoints);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Hosting failure: "+ex);
                return -1;
            }
            finally
            {
                runCompleteEvent.Set();
                core.Shutdown();
            }

            return 0;
        }

        /// <summary>
        /// If the parent process dies, so does the child. This cascades app pool recycles etc.
        /// </summary>
        private static void Core_PeriodicCheck(object sender, EventArgs e)
        {
            try {
                using (var proc = Process.GetProcessById(ParentProcessId))
                {
                    if (proc.HasExited) {
                        cancellationTokenSource.Cancel();
                    }
                }
            } catch (ArgumentException) {
                // No process found. Assume that means it's dead?
                cancellationTokenSource.Cancel();
            }
        }
    }
}