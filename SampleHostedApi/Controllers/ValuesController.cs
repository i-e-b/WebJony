using System;
using System.Collections.Generic;
using System.Web.Http;

namespace SampleHostedApi.Controllers
{
    [RoutePrefix("values")]
    public class ValuesController : ApiController
    {
        // GET api/values
        [Route("")]
        public IEnumerable<string> Get()
        {
            return new[] { "This is the value from", "version 3" };
        }

        // GET api/values/5
        [Route("{id}")]
        public string Get(int id)
        {
            //return "value";
            throw new Exception("I asploded :-(");
        }

        // POST api/values
        [Route("")]
        public string Post([FromBody]string value)
        {
            return "OK, I got that";
        }

        // PUT api/values/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        public void Delete(int id)
        {
        }
    }
}
