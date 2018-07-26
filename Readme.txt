Wrapper Test
============

Example of a web app that loads various other MVC Web API sites, and provides various security, logging and API versioning tools.

Features:
---------

* [x] Global error handling
* [x] AAD security unwrapping
* [x] watcher/loader
* [x] Real routing to services
* [x] Expose SwaggerUI calls
* [x] Signed-binary upload tool, require signed binaries on host. (note: the cert used is found by fixed name: "WrapperSigning.pfx.cer")
* [x] Handle differing versions of `System.Web.Http` in different versions of hosted site
* [x] Ensure that redirects work (change `localhost` bounces up to proxy parent)
* [x] Edit target site version to set <configuration>,  <system.web>, <customErrors mode="Off" />
* [x] Collect security and version header in Swagger
* [x] Keep old versions running until new version is warm (plus pre-warm with a call to '/health')
* [x] Delete failed updates
* [x] Root-level health check (call to all versions' /health endpoint and display)
* [x] Error/failure statistics (parent gathers failures and provides them on the health endpoint)
* [x] Optional force HTTPS upgrade
* [x] Ensure upload tool works if given a http endpoint and the target auto-upgrades
* [x] Rapid call test in JwtEndpointCaller (multithread calls)
* [x] Reject known dll names from the file system scan (speed up start-up)
* [x] Remove hard-coding, push account keys to a non-committed file

TODO:
-----

* [ ] Simple hosting on IIS (Custom version of RawIsapi project)
* [ ] Analytics integration point, including an Azure App Insights example
* [ ] Automatic result compression
* [ ] Centralised data store (for Azure hosting)
* [ ] New version feed-in mode? (newer minor versions get progressively more traffic?)
* [ ] Automatic A/B testing (50% feed, watch error rates)
* [ ] Version limit / retiring (limit on how many major versions can be active at once, with oldest being inaccessible)
* [ ] Status API
* [ ] Stats API (including graph output urls)
* [ ] Removal of specific versions? (delete by major & minor)
* [ ] Some graphs -- one that shows % proportion of versions over time, and the other where you pick a version and it shows % errors over time
* [ ] Options for CORS header support
* [ ] Gasconade version page
* [ ] Sample Service Fabric deployment

Potential features:
-------------------

* [ ] Ability to point different versions at different web.config transforms (i.e. latest version can be set to test db until stabilised)
* [ ] Ability to host function dlls (not using Huygens?)
* [ ] Static 'oops' result for 500-class errors in hosted site?
* [ ] Delete uploads that are out-of-date (if the version table rejects them)
* [ ] Optional service bus trace listener, or configurable listener injection
* [ ] Optional default version (that can we switched while live) -- for websites.
* [ ] Per-client rate limiting (rate limit how much any single IP can push/pull)
* [ ] White list upload IPs

Notes:
------

Request versioning is done with a header, with a major version only:
```
GET /user/1 HTTP/1.1
    Host: myapplication.com
    Accept: application/json
    Version: 1
```

Using AAD security requires supplying your own `security.json` file. An example is provided.

Application versioning is done with a combination of build number and API major version.
So like `[ApplicationSetupPoint(Major:3)]` and a build number of `2018.2.10.1`, the entire build number would be the minor.
You would see it described in the system as `3-2018.2.10.1`

Only the latest of each major version is kept, any earlier ones are rejected.
