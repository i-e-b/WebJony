using System.Web.Http;
using Swashbuckle.Application;
using SampleHostedApi.Controllers;

namespace SampleHostedApi
{
    public class SwaggerConfig
    {
        public static void Register()
        {
            var thisAssembly = typeof(ValuesController).Assembly;

            GlobalConfiguration.Configuration
                .EnableSwagger(c =>
                {
                    c.SingleApiVersion("v1", "SampleHostedApi").Description("API description (finally added in version 3)");
                })
                .EnableSwaggerUi(c =>
                {
                    c.DocumentTitle("Title from version 3");
                });
        }

        public static void RegisterDirect(HttpConfiguration config)
        {
            var thisAssembly = typeof(SwaggerConfig).Assembly;

            config
                .EnableSwagger(c => { c.SingleApiVersion("v1", "SampleHostedApi"); })
                .EnableSwaggerUi(c => { });
        }
    }
}
