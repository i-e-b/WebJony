using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Huygens;
using WrapperRoleListener.Internal;

namespace WrapperRoleListener.Core
{
    /// <summary>
    /// Wraps an MVC website for proxying.
    /// Handles modifying a site's config to support hosting, and starting up Huygens
    /// </summary>
    public class SiteHost
    {
        private long _failureCount;
        private long _callCount;

        /// <summary>
        /// Huygens hosted site
        /// </summary>
        public DirectServer HostedSite { get; set; }

        /// <summary>
        /// Last exception thrown during set-up, if any
        /// </summary>
        public Exception LastError { get; set; }

        /// <summary>
        /// Path of site being hosted
        /// </summary>
        public string TargetPath { get; set; }

        /// <summary>
        /// Major version of hosted site
        /// </summary>
        public int MajorVersion { get; set; }

        /// <summary>
        /// Full name of version being hosted
        /// </summary>
        public string VersionName { get; }

        /// <summary>
        /// Approximate historic view of successful calls
        /// </summary>
        public Timeslice SuccessHistory { get; set; }
        
        /// <summary>
        /// Approximate historic view of failed calls
        /// </summary>
        public Timeslice FailureHistory { get; set; }

        /// <summary>
        /// 0..1 proportion of calls that succeed
        /// </summary>
        public double SuccessRate {
            get {
                if (_callCount == 0) return 0;
                if (_failureCount == 0) return 1;
                return 1.0d - ((double)_failureCount / _callCount);
            }
        }

        /// <summary>
        /// Number of calls handled by this site
        /// </summary>
        public long CallCount { get { return _callCount; } }

        /// <summary>
        /// Start a site host
        /// </summary>
        /// <param name="rootAssemblyPath">Path to the assembly that contains a configuration point</param>
        /// <param name="majorVersion">Major version of site being hosted</param>
        /// <param name="versionName">Name of version</param>
        public SiteHost(string rootAssemblyPath, int majorVersion, string versionName)
        {
            MajorVersion = majorVersion;
            VersionName = versionName;
            TargetPath = rootAssemblyPath;

            SuccessHistory = new Timeslice();
            FailureHistory = new Timeslice();

            Trace.TraceInformation("Starting up site v" + MajorVersion + " at " + TargetPath);

            // Find paths
            if ( ! File.Exists(rootAssemblyPath)) throw new Exception("Root assembly path not accessible");
            var binPath = Path.GetDirectoryName(rootAssemblyPath);
            var outerDirectory = Path.GetDirectoryName(binPath);
            if (outerDirectory == null) throw new Exception("Could not guess outer hosting directory");

            // Try to edit child's Web.Config file for Azure hosting requirements. We also turn on debug information, so we can capture it for logging.
            var webConfigPath = Path.Combine(outerDirectory, "web.config");
            if (File.Exists(webConfigPath))
            {
                try
                {
                    EditForHosting(webConfigPath, binPath);
                }
                catch (Exception ex)
                {
                    LastError = ex;
                    Trace.TraceWarning("Failed to edit web.config: " + ex);
                }
            }
            else Trace.TraceWarning("Expected to see web config at '" + webConfigPath + "', but it was missing");

            // Chuck a copy of Huygens into the hosted site's bin directory.
            // This is required for hosting
            //if (!File.Exists(binPath + @"\Huygens.dll"))
            //    File.Copy(exePath + @".\Huygens.dll", binPath + @"\Huygens.dll", true); // try dropping in bin directly.

            Trace.TraceInformation("Loading and creating server at " + TargetPath);
            // Start the host
            HostedSite = new DirectServer(outerDirectory);

            Trace.TraceInformation("Warming server at " + TargetPath);
            WarmAndTest();

            Trace.TraceInformation("New version ready: v" + MajorVersion + " at " + TargetPath);
        }

        private void WarmAndTest()
        {
            // Give the site an initial kick, so it's warm when it goes into the version table
            var healthRequest = new SerialisableRequest
            {
                Headers = new Dictionary<string, string> {{"Accept", "text/plain,application/json"}},
                Method = "GET",
                RequestUri = "/health"
            };
            var result = HostedSite.DirectCall(healthRequest);

            if (result.StatusCode > 499)
            {
                // We don't care about 'bad request' or 'file not found', in case the health endpoint is not implemented
                // However, if the site fails we will entirely reject this version
                HostedSite.Dispose();
                throw new WarmupCallException("Version wake-up failed: v" + MajorVersion + " at " + TargetPath + "; " + result.StatusCode + " " + result.StatusMessage);
            }
        }

        /// <summary>
        /// Handle an incoming request using the hosted site
        /// </summary>
        public SerialisableResponse Request(SerialisableRequest request)
        {
            Interlocked.Increment(ref _callCount);
            var response = HostedSite.DirectCall(request);

            if (response.StatusCode > 499) {
                Interlocked.Increment(ref _failureCount);
                FailureHistory.Record();
            } else {
                SuccessHistory.Record();
            }

            return response;
        }

        private void EditForHosting(string confPath, string binPath)
        {
            var conf = XDocument.Load(confPath);

            SetProp(conf, "mode", "Off",             "system.web", "customErrors");
            SetProp(conf, "debug", "false",          "system.web", "compilation");

            // Attempt to directly bind Huygens:
            DirectBindHuygens(binPath, conf);

            conf.Save(confPath);
        }

        private static void DirectBindHuygens(string binPath, XDocument conf)
        {
            var alreadyDone = conf.Descendants().Any(e => e.Name.LocalName == "assemblyIdentity" && e.Attributes().Any(a => a.Value == "Huygens"));
            if (!alreadyDone)
            {
                var targ = conf.Descendants().First(e => e.Name.LocalName == "assemblyBinding");
                var rebind = new XElement(XName.Get("dependentAssembly", targ.Name.NamespaceName));
                var ident = new XElement(XName.Get("assemblyIdentity", targ.Name.NamespaceName));
                ident.SetAttributeValue("name", "Huygens");
                ident.SetAttributeValue("publicKeyToken", "null");
                ident.SetAttributeValue("culture", "neutral");
                var codebase = new XElement(XName.Get("codeBase", targ.Name.NamespaceName));
                codebase.SetAttributeValue("version", "1.0.0.0");
                codebase.SetAttributeValue("href", "file:///" + binPath.Replace('\\', '/').ToLowerInvariant() + "/huygens.dll");

                rebind.Add(ident);
                rebind.Add(codebase);
                targ.Add(rebind);
            }
        }

        private void SetProp(XDocument conf, string prop, string value, params string[] path)
        {

            XElement next = conf.Root;
            if (next == null) throw new Exception("Invalid web.config file");
            foreach (var child in path)
            {
                var targ = next.Elements().FirstOrDefault(e => e.Name.LocalName == child); // avoid issues with namespaces
                if (targ == null) {
                    next.Add(new XElement(XName.Get(child, next.Name.NamespaceName)));
                    targ = next.Elements().FirstOrDefault(e => e.Name.LocalName == child);
                }

                next = targ ?? throw new Exception("XML Edit failed");
            }

            var dest = next;
            dest.SetAttributeValue(prop, value);
        }

    }
}