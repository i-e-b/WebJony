using System;
using System.Web;
using System.Web.Http;
using WrapperMarkerAttributes;

namespace SampleHostedApi
{
    [ApplicationSetupPoint(ApiMajorVersion:3)]
    public class WebApiApplication : HttpApplication
    {

        /// <summary>
        /// Exposed configuration for running in any host
        /// </summary>
        public void ApplicationConfiguration(HttpConfiguration config) {
            WebApiConfig.Register(config);
            SwaggerConfig.RegisterDirect(config);
        }

        /// <summary>
        /// ASP.Net global configuration for running in IIS.
        /// This should *always* call into ApplicationConfiguration as below.
        /// </summary>
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(ApplicationConfiguration);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var exc = Server.GetLastError();
            Response.Write("<h2>Diagnostic:</h2>\n");
            Response.Write("<p>" + exc.Message + "</p>\n");
            Response.Write("<pre>"+exc.StackTrace+"</pre>");
            Response.Flush();
            Response.End();
        }
    }

}
