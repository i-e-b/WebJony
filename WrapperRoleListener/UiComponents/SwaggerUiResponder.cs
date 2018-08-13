using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Huygens;
using Huygens.Compatibility;
using SkinnyJson;
using WrapperCommon.AssemblyLoading;
using WrapperRoleListener.Core;

namespace WrapperRoleListener.UiComponents
{
    /// <summary>
    /// Handles requests to swagger ui components, including building the index page
    /// </summary>
    public static class SwaggerUiResponder
    {

        public static void HandleSwagger(IContext context, VersionTable<SiteHost> versions)
        {
            var path = context.Request.Url.AbsolutePath;
            if (path == "/swagger")
            {
                context.Response.Redirect("/swagger/");
                return;
            }
            if (path == "/swagger/")
            {
                HttpConverters.SendFile(@"swagger-ui\index.html", context);
                return;
            }
            if (path == "/swagger/wrapperLoadFunction.js")
            {
                // Query the loaded versions, build a JS document with the named versions to load
                BuildStartupFunction(context, versions);
                return;
            }
            if (path.StartsWith("/swagger/json")) // generator functions
            {
                // populate each version from the Huygens servers in the version table
                var requestedVersion = path.Substring(14);
                Trace.TraceInformation("Looking for version " + requestedVersion);
                SendApiSpecForVersion(context, versions, requestedVersion);
                return;

            }
            var guessPath = @"swagger-ui\" + (path.Substring(9).Replace("/", "\\"));
            Trace.TraceInformation("Trying to send a swagger file at '" + guessPath + "' from request '" + path + "'");
            HttpConverters.SendFile(guessPath, context);
        }

        private static void SendApiSpecForVersion(IContext context, VersionTable<SiteHost> versions, string requestedVersion)
        {
            var result = versions.GetExactVersion(requestedVersion);
            var sb = new StringBuilder(1024);
            if (result.IsFailure) {
                // Craft a special response
                AppendSwaggerErrorMessage(sb, result.FailureCause?.Message ?? "Unknown error");
                SendStringBuilder(context, sb);
                return;
            }
            var major = requestedVersion.Split('-').FirstOrDefault() ?? "0";

            // get a host setting to inject in the swagger
            var redirUri = new Uri(MainRequestHandler.ExternalEndpoint, UriKind.Absolute);
            var selfAuthority = new Uri(MainRequestHandler.ExternalEndpoint, UriKind.Absolute).Authority;
            if (MainRequestHandler.UpgradeHttp && redirUri.Scheme == "http")
            {
                selfAuthority = redirUri.Host; // configured address would get bounced, to try guess a better uri
            }

            var proxy = result.ResultData;
            var routes = new[] { "/swagger/docs/v1", "/swagger/v1/swagger.json", "/swagger/docs/v" + major, "/swagger/v" + major + "/swagger.json" }; // we expect the raw swagger JSON to be in one of these paths
            foreach (var route in routes)
            {
                var rq = new SerialisableRequest{
                    Method = "GET",
                    RequestUri = route,
                    Headers = new Dictionary<string, string>()
                };
                var tx = proxy.Request(rq);
                if (tx.StatusCode < 299) // found it
                {
                    var spec = Json.DefrostDynamic(Encoding.UTF8.GetString(tx.Content));

                    spec.host = selfAuthority;
                    spec.info.version = requestedVersion;

                    if      (MainRequestHandler.UpgradeHttp)    { spec.schemes = new[] {         "https" }; }
                    else if (MainRequestHandler.HttpsAvailable) { spec.schemes = new[] { "http", "https" }; }
                    else                                           { spec.schemes = new[] { "http"          }; }

                    spec.securityDefinitions = new Dictionary<string, object>{
                        { "Bearer Token", new { type="apiKey", name = "Authorization", @in = "header" } }
                    };

                    spec.security = new[] {
                        new Dictionary<string, object>{ { "Bearer Token", new object[0] } }
                    };

                    sb.Append(Json.Freeze(spec));

                    SendStringBuilder(context, sb);
                    return;
                }
            }
            
            AppendSwaggerErrorMessage(sb, "Failed to find a JSON endpoint in the hosted API. Make sure that hosted APIs respond to one of:<br/>" + string.Join("<br/>",routes));
            SendStringBuilder(context, sb);
        }

        private static void AppendSwaggerErrorMessage(StringBuilder sb, string message)
        {
            sb.Append("{\r\n\"swagger\": \"2.0\",\r\n\"info\": {\r\n\"version\": \"err\",\r\n\"title\": \"Swagger Proxy Failure\",\r\n\"description\": \"");
            sb.Append(message);
            sb.Append("<br/>Try <a href=\\\"/swagger/\\\">refreshing the page</a> to get the most recent versions\"\r\n},\"paths\":{}}");
        }

        private static void BuildStartupFunction(IContext context, VersionTable<SiteHost> versions)
        {
            var list = versions.VersionNames()?.ToList() ?? new List<string>();

            var sb = new StringBuilder(1024);
            sb.Append(@"window.onload = function () { var ui = SwaggerUIBundle({
                urls: [");

            if (list.Count < 1) {
                sb.Append("{ url: './json/0-0.0.0.0', name: 'No versions loaded!'},");
            }

            // add version links
            foreach (var vers in list)
            {
                sb.Append("{ url: './json/");
                sb.Append(vers);
                sb.Append("', name: '");
                sb.Append(vers);
                sb.Append("'},");
            }
            
            sb.Append(@"],");

            // Try to pass correct version header in 'try it out'
            sb.Append("requestInterceptor: function (req) {\r\n" +
                      "    if (! req.loadSpec) {\r\n" +
                      "        req.headers.Version = document.getElementById('select').selectedOptions[0].innerText.split(\'-\')[0];\r\n" +
                      "    }\r\n" +
                      "    return req;\r\n" +
                      "  },");


            sb.Append(@"
                dom_id: '#swagger-ui', deepLinking: true, presets: [ SwaggerUIBundle.presets.apis, SwaggerUIStandalonePreset ],
                plugins: [ SwaggerUIBundle.plugins.DownloadUrl ],
                layout: 'StandaloneLayout'
            }); 
            window.ui = ui;
        }");

            SendStringBuilder(context, sb);
        }

        private static void SendStringBuilder(IContext context, StringBuilder sb)
        {
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            context.Response.ContentLength64 = bytes.LongLength;
            context.Response.StatusCode = 200;
            context.Response.StatusDescription = "OK";
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
    }
}