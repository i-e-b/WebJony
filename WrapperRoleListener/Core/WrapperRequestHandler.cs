using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security;
using System.Text;
using System.Threading;
using Huygens.Compatibility;
using WrapperCommon;
using WrapperCommon.AssemblyLoading;
using WrapperCommon.Security;
using WrapperRoleListener.Internal;
using WrapperRoleListener.UiComponents;

namespace WrapperRoleListener.Core
{
    /// <summary>
    /// Handles all HTTP calls to the service, routing them to proxies or directly responding
    /// </summary>
    public class WrapperRequestHandler
    {
        private readonly ISecurityCheck _security;
        private readonly string _watchFolder;
        private readonly string _baseFolder;
        private readonly VersionTable<SiteHost> _versionTable;

        private TimeSpan _warmUp;
        private volatile bool _isScanning; // just for info, a version scan is in progress
        private volatile bool _firstScan; // Are we in the initial warm-up?
        private Exception _lastScanError;

        public PluginScanner AvailableAppScanner { get; set; }

        public WrapperRequestHandler(ISecurityCheck security)
        {
            _security = security;
            
            var timer = new Stopwatch();
            timer.Start();
            _versionTable = new VersionTable<SiteHost>();

            _baseFolder = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.Exists(CoreListener.AnySetting("HostedSitesRootDirectory")))
            {
                // use directly specified folder
                _watchFolder = CoreListener.AnySetting("HostedSitesRootDirectory");
            }
            else
            {
                // use general working folder (best for Azure?)
                _watchFolder = Path.Combine(_baseFolder, "uploads");
                Directory.CreateDirectory(_watchFolder);
            }
            
            AvailableAppScanner = new PluginScanner(_watchFolder);
            AvailableAppScanner.PluginsChanged += AvailableAppWatcher_PluginsChanged;

            // Load initial versions out-of-band so we don't appear dead at start-up
            new Thread(() =>
            {
                try
                {
                    _firstScan = true;
                    Trace.TraceInformation("Initial scan started");
                    AvailableAppScanner.RefreshPlugins();
                    Trace.TraceInformation("Initial scan complete");
                }
                finally
                {
                    _firstScan = false;
                    timer.Stop();
                    _warmUp = timer.Elapsed;
                    Console.WriteLine("Ready");
                }
            })
            { IsBackground = true }.Start();
        }

        private void AvailableAppWatcher_PluginsChanged(object sender, PluginsChangedEventArgs<string> e)
        {
            _isScanning = true;
            try
            {
                _versionTable.ClearRemovedVersions();
                foreach (var path in AvailableAppScanner.CurrentlyAvailable)
                {
                    try
                    {
                        _versionTable.SubmitVersion(path, CreateServer);
                    }
                    catch (DelayRescanException)
                    {
                        // If lots of files are being removed, wait for them and try again
                        Thread.Sleep(500);
                    }
                    catch (Exception ex)
                    {
                        _lastScanError = ex;
                        if (!_firstScan) throw;
                    }
                }
            }
            finally
            {
                _isScanning = false;
            }
        }

        private SiteHost CreateServer(string typePath, string versionName, int majorVersion)
        {
            return new SiteHost(typePath, majorVersion, versionName);
        }

        public void Handle(IContext context)
        {
            if (CoreListener.UpgradeHttp && !context.Request.IsSecureConnection)
            {
                UpgradeResponse(context);
                return;
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var closedOk = false;
            try
            {
                var version = context.Request.ProtocolVersion; // 1.1 or higher is definitely preferred, but a *lot* of bots declare 1.0
                Stream outputBuf;
                if (ChunkedOutputSupported(version))
                {
                    context.Response.SendChunked = true; // Cheat: we don't have to set a content-length, or buffer output.
                    outputBuf = context.Response.OutputStream;
                }
                else
                {
                    outputBuf = new MemoryStream();
                }

                // Do the core call
                TraceRequest(context);

                InnerHandler(context, outputBuf);

                if (outputBuf is MemoryStream buffer)
                {
                    buffer.Seek(0, SeekOrigin.Begin);
                    if (buffer.Length > 0)
                    {
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.OutputStream.Write(buffer.GetBuffer(), 0, (int)buffer.Length);
                    }
                }

                context.Response.Close();
                closedOk = true;

                stopwatch.Stop();

                Trace.TraceInformation("Complete OK. Overall: " + stopwatch.Elapsed);
            }
            catch (ProtocolViolationException pex) // happens when we do something that the HTTP listener doesn't like.
            {
                stopwatch.Stop();
                Trace.TraceInformation("Complete Failed: " + stopwatch.Elapsed + "\r\n" + pex);
            }
            catch (ThreadAbortException)
            {
                // Do nothing. IIS is weird.
                Trace.TraceInformation("ThreadAbortException");
            }
            catch (Exception ex)
            {
                try
                {
                    context.Response.StatusCode = 500;
                }
                catch
                {
                    Ignore();
                }
                stopwatch.Stop();
                Trace.TraceInformation("Complete Failed: " + stopwatch.Elapsed + "\r\n" + ex);
            }
            finally
            {
                stopwatch.Stop();
                // Must always close the response, otherwise the request will hang.
                if (!closedOk) context.Response.Close();
            }
        }

        private void UpgradeResponse(IContext context)
        {
            var baseUri = new Uri(CoreListener.ExternalEndpoint, UriKind.Absolute);
            if (baseUri.Scheme == "https") {
                context.Response.Redirect($"{baseUri.GetLeftPart(UriPartial.Authority)}/{context.Request.Url.PathAndQuery}"); // hope that the default port will work
            } else {
                context.Response.Redirect($"https://{baseUri.Host}{context.Request.Url.PathAndQuery}"); // hope that the default port will work
            }
            context.Response.Close();
        }

        private static void Ignore() { }

        private static void TraceRequest(IContext context)
        {
            Trace.TraceInformation("Handling:\r\n        "
                                   + context.Request.HttpMethod + " " + context.Request.RawUrl + " HTTP/" + context.Request.ProtocolVersion + "\r\n        "
                                   + "Host: " + context.Request.Headers.Get("Host"));
        }

        private static bool ChunkedOutputSupported(Version version)
        {
            if (version.Major > 1) return true;
            if (version.Major == 1 && version.Minor > 0) return true;
            return false;
        }

        /// <summary>
        /// This is where the routing happens
        /// </summary>
        private void InnerHandler(IContext context, Stream output)
        {
            var request = context.Request;
            var response = context.Response;

            if (HandleSpecialPaths(context, output)) return; // special unsecured paths

            if (CheckSecurity(context, output)) return;

            if (FindMatchingVersion(response, request, output, out var proxy)) return;
            //var proxy = _versionTable.AllVersions().First(); // TEST: Use first version

            try
            {
                //Trace.TraceInformation("Converting and handling (" + proxy.TargetPath + ")");

                var sreq = HttpConverters.ConvertToSerialisable(request);

                var sw_core = new Stopwatch();
                sw_core.Start();
                var result = proxy.Request(sreq);
                sw_core.Stop();
                
                HttpConverters.CopyToHttpListener(result, response, CoreListener.ExternalEndpoint);

                Trace.TraceInformation("Call complete. Core timing: " + sw_core.Elapsed);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Call failed: " + ex);
                InternalFailure(response, ex, proxy, output);
            }
        }

        private bool FindMatchingVersion(IResponse response, IRequest request, Stream output, out SiteHost proxy)
        {
            //Trace.TraceInformation("Currently loaded: " + _versionTable.VersionsAvailable());
            proxy = null;
            if (_versionTable.IsEmpty())
            {
                Trace.TraceInformation("Rejecting - no loaded APIs");
                response.StatusCode = 503;
                response.ContentType = "text/plain";
                Write(output, "\r\nRejected: no APIs are loaded into the host");
                return true;
            }

            var version = request.Headers.Get("Version");
            var proxyCheck = _versionTable.GetForVersionHeader(version);

            if (proxyCheck == null || proxyCheck.IsFailure)
            {
                Trace.TraceInformation("Rejecting - no matching API");
                response.StatusCode = 400;
                response.ContentType = "text/plain";
                Write(output, "\r\nRejected: You must supply a 'Version' header for a known major version");
                Write(output, "\r\nCurrently loaded: " + _versionTable.VersionsAvailable());
                return true;
            }

            proxy = proxyCheck.ResultData;
            return false;
        }

        private bool CheckSecurity(IContext context, Stream output)
        {
            var response = context.Response;
            if (_security.Validate(context) == SecurityOutcome.Fail)
            {
                Trace.TraceInformation("Rejecting - security fail");
                response.StatusCode = 401;
                response.ContentType = "text/plain";
                Write(output, "\r\nRejected: you didn't say the magic word");
                Write(output, "\r\nCurrently loaded: " + _versionTable.VersionsAvailable());
                return true;
            }

            return false;
        }

        private bool HandleSpecialPaths(IContext context, Stream output)
        {
            var request = context.Request;
            var response = context.Response;

            // TODO: capture a few special paths (like Swagger, Gasconade, healthcheck etc)
            if (request.Url.PathAndQuery == "/favicon.ico")
            {
                Trace.TraceInformation("Ignoring a favicon request");
                response.StatusCode = 410;
                return true;
            }

            if (request.Url.PathAndQuery.StartsWith("/swagger"))
            {
                SwaggerUiResponder.HandleSwagger(context, _versionTable); // this one bypasses the content buffer, as it's sending known-length files
                return true;
            }

            if (request.Url.PathAndQuery == "/test")
            {
                SelfTestMessage(response, output);
                return true;
            }

            if (request.Url.PathAndQuery == "/shutdown")
            {
                ShutdownProxies(request, response, output);
                return true;
            }

            if (request.Url.PathAndQuery == "/upload")
            {
                AcceptNewVersion(request, response, output);
                return true;
            }

            return false;
        }

        private static void Write(Stream response, string str)
        {
            var resp = Encoding.UTF8.GetBytes(str);
            response.Write(resp, 0, resp.Length);
        }

        private static void InternalFailure(IResponse response, Exception ex, SiteHost conn, Stream output)
        {
            response.StatusCode = 500;
            Write(output, "Failure in the proxy parent:\r\n" + ex);
            if (conn.LastError != null) {
                Write(output, "The site host has error: " + conn.LastError);
            }
            response.StatusDescription = "Internal Server Error";
        }
        
        private void ShutdownProxies(IRequest request, IResponse response, Stream output)
        {
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = 400;
                Write(output, "This must be called as a POST");
                return;
            }

            var errs = ShutdownAll();

            if (string.IsNullOrEmpty(errs))
            {
                response.StatusCode = 200;
                response.StatusDescription = "OK";
                Write(output, "All proxies shut down. They may be restarted by subsequent calls.");
            }
            else
            {
                response.StatusCode = 500;
                response.StatusDescription = "Internal Server Error";
                Write(output, "Some proxies errored during shutdown. They have been force terminated:\r\n" + errs);
            }
        }

        private void AcceptNewVersion(IRequest request, IResponse response, Stream output)
        {
            lock (_versionTable)
            {
                if (request.HttpMethod == "SEARCH")
                {
                    AvailableAppScanner.RefreshPlugins();
                    response.StatusCode = 200;
                    response.StatusDescription = "OK";
                    Write(output, "Rescan triggered. New version will be available if applicable.");
                    return;
                }

                if (request.HttpMethod != "POST")
                {
                    response.StatusCode = 400;
                    Write(output, "Only POST and SEARCH methods accepted");
                    return;
                }

                var expectedCert = Path.Combine(_baseFolder, "WrapperSigning.pfx.cer");
                var baseDir = Path.Combine(_watchFolder, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff"));
                try
                {
                    if (!File.Exists(expectedCert))
                    {
                        throw new Exception("Validation certificate not found");
                    }
                    Directory.CreateDirectory(baseDir);

                    var archiveFile = Path.Combine(baseDir, "upload.inpkg");
                    using (var file = File.OpenWrite(archiveFile))
                    {
                        // ReSharper disable once AccessToDisposedClosure
                        Sync.Run(() => request.InputStream.CopyToAsync(file));
                    }
                    SimpleCompress.Decompress.FromFileToFolder(archiveFile, baseDir, expectedCert);
                    File.Delete(archiveFile);

                    // Try to kick the new version off straight away
                    AvailableAppScanner.RefreshTarget(baseDir);

                    response.StatusCode = 202;
                    response.StatusDescription = "Accepted";
                    Write(output, "Upload accepted. New version will be available if applicable.");
                }
                catch (SecurityException sec)
                {
                    try { Directory.Delete(baseDir, true); } catch { Ignore(); }
                    response.StatusCode = 403;
                    response.StatusDescription = "Signature Unacceptable";
                    Write(output, "Rejected upload due to signature failure: " + sec);
                }
                catch (WarmupCallException wex)
                {
                    try { Directory.Delete(baseDir, true); } catch { Ignore(); }
                    response.StatusCode = 400;
                    response.StatusDescription = "Uploaded version failed health check: " + wex.Message;
                    Write(output, "Upload failed health check: " + wex);
                }
                catch (Exception ex)
                {
                    try { Directory.Delete(baseDir, true); } catch { Ignore(); }
                    response.StatusCode = 500;
                    response.StatusDescription = "Internal Server Error";
                    Write(output, "Failed to accept upload: " + ex);
                }
            }

        }

        /// <summary>
        /// Shut down all proxies
        /// </summary>
        public string ShutdownAll()
        {
            var errs = new StringBuilder();
            foreach (var conn in _versionTable.AllVersions())
            {
                try
                {
                    conn?.HostedSite?.Dispose();
                }
                catch (Exception ex)
                {
                    errs.AppendLine(ex.ToString());
                }
            }
            return errs.ToString();
        }

        private void SelfTestMessage(IResponse response, Stream output)
        {
            response.StatusCode = 200;
            response.StatusDescription = "OK";
            response.ContentType = "text/html";

            Write(output, TestPageGenerator.Generate(_versionTable, _warmUp, _watchFolder, _isScanning, _lastScanError));
        }
    }
}