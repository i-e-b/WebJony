using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using DispatchSharp;
using Huygens.Compatibility;
using WrapperCommon.Azure;
using WrapperCommon.Security;
using WrapperRoleListener.Internal;

namespace WrapperRoleListener.Core
{
    /// <summary>
    /// Handles waiting for HTTP messages and dispatching them when running as an Azure Worker, or as a plain exe.
    /// </summary>
    public class ListenerLoop
    {
        /// <summary>
        /// Number of requests/threads to handle at once
        /// </summary>
        public const int Parallelism = 20;

        /// <summary>
        /// Event triggered periodically while the listener loop is running.
        /// You can use this to test shutdown conditions. This will not be called
        /// when the server is busy with requests. Don't do long running work in this handler.
        /// </summary>
        public event EventHandler PeriodicCheck;

        private readonly MainRequestHandler _handler;
        private IDispatch<IContext> _dispatcher;

        public ListenerLoop()
        {
            // Start re-populating signing keys. If the code-cached keys are out of date, it may take a few seconds to freshen.
            SigningKeys.UpdateKeyCache();

            // Tracing setup
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += BaseExceptionHandler;

            Trace.UseGlobalLock = false;
            Trace.AutoFlush = false;
            Trace.Listeners.Add(LocalTrace.Instance);

            Trace.TraceInformation("WrapperRoleListener coming up");
            Trace.TraceInformation("Old connection limit was " + ServicePointManager.DefaultConnectionLimit);
            ServicePointManager.DefaultConnectionLimit = Parallelism;
            Trace.TraceInformation("New connection limit is " + ServicePointManager.DefaultConnectionLimit);

            ServicePointManager.ReusePort = true;
            ServicePointManager.EnableDnsRoundRobin = true; // can load balance with DNS
            ServicePointManager.SetTcpKeepAlive(false, 0, 0);

            _handler = new MainRequestHandler(new AadSecurityCheck());

        }

        public void Listen(CancellationToken token, IEnumerable<Endpoint> endpoints)
        {
            ThreadPool.SetMaxThreads(Parallelism, Parallelism);
            ThreadPool.SetMinThreads(1, 1);

            _dispatcher = Dispatch<IContext>.CreateDefaultMultithreaded("ResponderThreads", Parallelism);
            _dispatcher.AddConsumer(_handler.Handle);
            _dispatcher.Start(); // To use thread pool instead of dispatcher: don't call this and change the code in ListenerCallback

            var listener = new HttpListener();

            Trace.TraceInformation("Binding...");
            MainRequestHandler.HttpsAvailable = false;
            foreach (var endpoint in endpoints)
            {
                try
                {
                    var name = endpoint.Name;
                    var baseUri = $"{endpoint.Protocol}://{endpoint.IPEndpoint}/";

                    if (endpoint.Protocol == "https") MainRequestHandler.HttpsAvailable = true;

                    listener.Prefixes.Add(baseUri);

                    Trace.TraceInformation("Adding listener for '" + name + "' on " + baseUri);
                }
                catch (Exception ex)
                {
                    Trace.Fail("BINDING FAILED! " + ex);
                    return;
                }
            }

            Trace.TraceInformation("Starting...");
            try
            {
                listener.IgnoreWriteExceptions = false;//true;
                listener.Start();
            }
            catch (Exception ex)
            {
                Trace.Fail("STARTING FAILED! " + ex);
                return;
            }
            Trace.TraceInformation("Started");


            while (!token.IsCancellationRequested)
            {
                var asctx = listener.BeginGetContext(ListenerCallback, listener);
                var gotOne = asctx.AsyncWaitHandle.WaitOne(1500, true); // break out of wait loop to allow cancellation to happen.

                if (gotOne) Trace.TraceInformation("Incoming message");
                else OnPeriodicCheck();
            }

            listener.Stop();
            listener.Close();
            _dispatcher.Stop();
        }
        

        protected virtual void OnPeriodicCheck()
        {
            PeriodicCheck?.Invoke(this, EventArgs.Empty);
        }

        private static void BaseExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            var e = (Exception)args.ExceptionObject;
            Trace.TraceError("MyHandler caught : " + e);
            Trace.TraceError("Runtime terminating: {0}", args.IsTerminating);
        }

        /// <summary>
        /// Submit HTTP context to worker pool
        /// </summary>
        private void ListenerCallback(IAsyncResult result)
        {
            var listener = result.AsyncState as HttpListener;
            try {
                var context = listener?.EndGetContext(result);
                AddWork(context.Wrap());
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error receiving connection: " + ex);
            }
        }

        public void Shutdown()
        {
            _dispatcher.Stop();
            _handler.ShutdownAll();
        }

        internal void AddWork(IContext context)
        {
            // ** HERE ** is where we assign work
            // Using dispatch-sharp:
            if (context != null) _dispatcher.AddWork(context);

            // using .net thread pool:
            /*ThreadPool.QueueUserWorkItem(c =>
            {
                _handler.Handle(context);
            }, context);*/
        }

        public void HandleSynchronous(IContext wrap)
        {
            _handler.Handle(wrap);
        }

        public void Rescan(){
            _handler.AvailableAppScanner.RefreshPlugins();
        }
    }
}