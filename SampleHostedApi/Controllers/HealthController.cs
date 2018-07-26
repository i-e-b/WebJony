using System.Web.Http;

namespace SampleHostedApi.Controllers
{
    [RoutePrefix("health")]
    public class HealthController : ApiController
    {
        [Route("")]
        public string Get()
        {
            return "Everything is OK";
        }

        [Route("History")]
        public string GetHistory() {
            return "This endpoint does nothing, and is removed in the next version";
        }
    }
}