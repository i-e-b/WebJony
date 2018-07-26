using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.WindowsAzure.ServiceRuntime;
using WrapperRoleListener.Core;

namespace WrapperRoleListener
{
    /// <summary>
    /// Binds a HTTP listener to the ports configured in the service definition,
    /// passing requests to a WrapperRequestHandler instance
    /// </summary>
    public class AzureEntryPoint : RoleEntryPoint
    {

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
        private CoreListener core;

        public override void Run()
        {
            Trace.UseGlobalLock = false;
            Trace.TraceInformation("WrapperRoleListener 'run' was requested");
            var endpoints = new List<Endpoint>();

            HardReference<Microsoft.WindowsAzure.Diagnostics.DiagnosticMonitorTraceListener>();

            try
            {
                foreach (var pair in RoleEnvironment.CurrentRoleInstance.InstanceEndpoints)
                {
                    endpoints.Add(new Endpoint{
                        Name = pair.Key,
                        Protocol = pair.Value.Protocol,
                        IPEndpoint = pair.Value.IPEndpoint.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to read endpoint configuration: " + ex);
                throw;
            }
            try
            {
                core = new CoreListener();
                core.Listen(cancellationTokenSource.Token, endpoints);
            }
            finally
            {
                runCompleteEvent.Set();
            }
        }

        /// <summary>
        /// This is here to ensure we have a hard reference to things implicitly required by Azure
        /// </summary>
        private void HardReference<T>()
        {
            Trace.Assert(typeof(T).Name != "?", "Never happens");
        }

        public override bool OnStart()
        {
            var result = base.OnStart();
            Trace.TraceInformation("WrapperRoleListener has been started");
            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("WrapperRoleListener stopping");
            core.Shutdown();
            cancellationTokenSource.Cancel();
            runCompleteEvent.WaitOne();
            base.OnStop();
            Trace.TraceInformation("WrapperRoleListener has stopped");
        }


    }
}
