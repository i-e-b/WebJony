using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Huygens;
using Tag;
using WrapperRoleListener.Core;
using WrapperRoleListener.Internal;
using WrapperRoleListener.Internal.AssemblyLoading;

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
                T.g("head")[
                    T.g("title")["Wrapper proxy test page"],
                    T.g("style")[".good {stroke: #0A0; } .bad {stroke: #A00; } path { stroke-width: 2.5px; fill: none;  opacity: 0.5;}"]
                ],
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


            // Some graphs
            body.Add(T.g("h1")["Recent History"], T.g("p")["Logarithmic time scale. Left is last few seconds, right is last week."]);

            foreach (var version in list)
            {
                body.Add(T.g("h3")["Version " + version.MajorVersion]);
                body.Add(T.g("div")[RenderGraph(version.SuccessHistory, version.FailureHistory)]);
            }

            return page.ToString();
        }

        private static TagContent RenderGraph(Timeslice success, Timeslice failure){
            var height = 240;
            var width = 320;

            var doc = SvgHeader(width, height, out var root);

            var good = success.View().Values.ToArray();
            var bad = failure.View().Values.ToArray();
            var allMax = Math.Max(1, Math.Max(good.Max(), bad.Max()));
            DrawLines(good, allMax, height, width, root, "good");
            DrawLines(bad,  allMax, height, width, root, "bad");

            return doc;
        }

        private static void DrawLines(double[] hist, double max, int height, int width, TagContent root, string @class)
        {
            var count = hist.Length;

            var Hprop = Math.Max(1, max / height);
            var Vprop = (double) width / count;
            for (int i = 1; i < count; i++)
            {
                var Ly = hist[i - 1] / Hprop;
                var Lx = (i - 1) * Vprop;
                var Ry = hist[i] / Hprop;
                var Rx = i * Vprop;

                root.Add(SimpleLine(Lx, height - Ly, Rx, height - Ry, @class));
            }
        }

        private static TagContent SimpleLine(double x1, double y1, double x2, double y2, string @class) {
            return T.g("g", "class",@class)[
                T.g("path", "d",
                    "M"+x1+","+y1+
                    "L"+x2+","+y2)
            ];
        }

        private static TagContent SvgHeader(int width, int height, out TagContent rootElement){
            rootElement = T.g("g", "transform","translate(0,0)");
            return T.g("svg", "id", "svgroot", "width", width + "px", "height", height + "px")[rootElement];
        }
    }
}