using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Huygens;
using Tag;
using WrapperCommon.AssemblyLoading;
using WrapperRoleListener.Core;
using WrapperRoleListener.Internal;

namespace WrapperRoleListener.UiComponents
{
    public class TestPageGenerator
    {
        private static readonly SerialisableRequest HealthRequest = new SerialisableRequest
        {
            Headers = new Dictionary<string, string> { { "Accept", "text/plain,application/json" } },
            Method = "GET",
            RequestUri = "/health"
        };

        public static string Generate(VersionTable<SiteHost> _versionTable, TimeSpan _warmUp, string _watchFolder, bool _isScanning, Exception _lastScanError)
        {
            var body = T.g("body");
            var page = T.g("html")[
                T.g("head")[T.g("title")["Wrapper proxy test page"]],
                body
            ];

            body.Add(T.g("h1")["Status"]);
            if (_warmUp.Ticks == 0)
            {
                body.Add(T.g("p")["The proxy is starting up. Versions will be listed below as they are ready."]);
            }
            else
            {
                body.Add(T.g("p")["Three flavours"]);
                body.Add(T.g("p")["The proxy is active. Initial warm up took: " + _warmUp]);
            }
            body.Add(T.g("p")["Currently loaded: " + _versionTable.VersionsAvailable()]);
            body.Add(T.g("p")["Watch folder: ", T.g("tt")[_watchFolder], " ", (_isScanning) ? ("Scan is in progress") : ("Scanner is idle")]);

            // ReSharper disable once InconsistentlySynchronizedField
            if (_lastScanError != null) {
                body.Add(T.g("p")["Last scan error: ", _lastScanError.ToString()]);
            }

            // run a health check against all versions and spit them out here...
            body.Add(T.g("h1")["Health check"]);
            var list = _versionTable.AllVersions().ToList();
            foreach (var version in list)
            {
                var result = version.HostedSite.DirectCall(HealthRequest);
                body.Add(T.g("h3")["Version " + version.VersionName]);
                body.Add(T.g("p")[version.CallCount + " calls, " + (100 * version.SuccessRate).ToString("0.00") + "% successful"]);
                body.Add(T.g("p")[result.StatusCode + " " + result.StatusMessage]);

                if (result.Content != null) body.Add(T.g("pre")[Encoding.UTF8.GetString(result.Content)]);

                if (version.LastError != null)
                {
                    body.Add(T.g("p")["Last proxy error: " + version.LastError]);
                }
            }

            
            body.Add(T.g("h1")["Recent log entries"]);
            // This double container lets us put the scroll-bar on the left. I just like it better that way :-)
            body.Add(T.g("div", "style","direction:rtl;overflow-y:scroll;height:40%;")[
                T.g("pre", "style","direction: ltr;")[LocalTrace.ReadAll()]
            ]);


            // *VERY* experimental:
            body.Add(T.g("h1")["Experimental history"]);

            foreach (var version in list)
            {
                body.Add(T.g("h3")[version.MajorVersion.ToString()]);
                var pre = T.g("pre");
                var hist = version.SuccessHistory.View();
                foreach (var pair in hist)
                {
                    pre.Add("\r\n" + pair.Key + " -> " + pair.Value);
                }
                body.Add(pre);
            }

            return page.ToString();
        }
    }
}