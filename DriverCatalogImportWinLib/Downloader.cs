using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    internal class Downloader
    {
        private readonly HttpClient primaryClient;
        private readonly HttpClient secondaryClient = null;
        private readonly ILogger logger;

        private readonly IDirFinder dirFinder;

        public Downloader(ILogger _logger, IDirFinder _dirFinder) : this (_logger, _dirFinder, null) { }
        public Downloader(ILogger _logger, IDirFinder _dirFinder, Uri _proxyUri)
        {
            logger = _logger;
            dirFinder = _dirFinder;

            if (_proxyUri != null)
            {
                logger.LogInformation("Proxy setting is provided in config file");

                var proxy = new WebProxy
                {
                    //Address = new Uri(@"http://192.168.50.2:8080")
                    Address = _proxyUri
                };
                var httpClientHandler = new HttpClientHandler
                {
                    Proxy = proxy
                };
                primaryClient = new HttpClient(httpClientHandler);
                secondaryClient = new HttpClient();
                
                string httpsProxy = Environment.GetEnvironmentVariable("HTTPS_PROXY");
                string httpProxy = Environment.GetEnvironmentVariable("HTTP_PROXY");
                logger.LogCritical("System default proxy setting: HTTP proxy: {ip1},   HTTPS proxy: {ip2}  ", httpProxy, httpsProxy);
            }
            else
            {
                primaryClient = new HttpClient();
                logger.LogInformation("Proxy setting is not provided in the config file");
                logger.LogTrace("If environment variables HTTP_PROXY or HTTPS_PROXY are set, their values will be used");
            }
            
            // HttpClient has a static member "DefaultProxy", client created by "new HttpClient()" inherits it.
            // at this point, the DefaultProxy doesn't have an address.
            // When client actually sends a requests, "DefaultProxy" will read environment variable settings
        }

        private HttpRequestMessage BuildRequest(VendorProfile vp)
        {
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, vp.DownloadUri);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue("Other")));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(@"text/html"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(@"application/xhtml+xml"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(@"application/xml", 0.9));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(@"image/webp"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(@"image/apng"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(@"*/*", 0.8));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(@"application/signed-exchange", 0.7));
            req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
            return req;
        }

        private bool UpdateContentLength(HttpResponseMessage resp, VendorProfile vp)
        {
            HttpContentHeaders headers = resp.Content.Headers;
            IEnumerable<string> values = headers.GetValues("Content-Length");
            if (values.Count() <= 0)
            {
                logger.LogError("[{vn}] : No Content-Length field in the content header", vp.Name);
                return false;
            }
            if (values.Count() > 1)
            {
                logger.LogWarning("[{vn}] : Multiple Content-Length fields in the content header", vp.Name);
            }
            string[] aValues = Enumerable.ToArray<string>(values);
            try
            {
                vp.NewContentLength = int.Parse(aValues[0], NumberStyles.Integer);
                logger.LogInformation("[{vn}] : Content-Length: {len}", vp.Name, vp.NewContentLength);
            }
            catch (Exception e)
            {
                logger.LogError(e, "[{vn}] : Invalid value for Content-Length field, {v}\n", vp.Name, aValues[0]);
                return false;
            }
            return true;
        }

        public async Task<bool> DownloadCab(VendorProfile vp)
        {
            HttpResponseMessage resp = null;
            try
            {
                HttpRequestMessage req = BuildRequest(vp);
                resp = await primaryClient.SendAsync(req);
                try
                {
                    resp.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException e)
                {
                    logger.LogError(e, "[{vendorname}] : Fail to download cab file\n", vp.Name);
                    return false;
                }
            }
            catch (HttpRequestException e)
            {
                logger.LogDebug("[{vn}] : exception: {ex} ", vp.Name, e.Message);
                if (secondaryClient != null)
                {
                    logger.LogWarning("[{vn}] : Download attempt timed out with proxy setting in config file, try with no proxy or system default proxy setting now", vp.Name);

                    HttpRequestMessage req = BuildRequest(vp); // is there a way I can reset a request message so that I don't have to re-build one?
                    resp = await secondaryClient.SendAsync(req);
                    try
                    {
                        resp.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException e2)
                    {
                        logger.LogError(e2, "[{vendorname}] : Fail to download cab file\n", vp.Name);
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            logger.LogDebug("[{vendorname}] : Downloaded cab file", vp.Name);
            string downloadedCabFilePath = Path.Combine(dirFinder.GetNewCabFileDir(), vp.CabFileName);
            using (FileStream fs = File.Create(downloadedCabFilePath))
            {
                var t = resp.Content.CopyToAsync(fs);
                await t;
                if (t.IsCompleted)
                {
                    logger.LogDebug("[{vendorname}] : Saved cab file", vp.Name);
                    UpdateContentLength(resp, vp);
                    resp.Dispose();
                    return true;
                }
                else
                {
                    logger.LogError("[{vendorname}] : Saved cab file", vp.Name);
                    UpdateContentLength(resp, vp);
                    resp.Dispose();
                    return false;
                }
            }
        }

        public async Task<bool> CheckCabSize(VendorProfile vp)
        {
            HttpResponseMessage resp = null;
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Head, vp.DownloadUri);
                resp = await primaryClient.SendAsync(req);
                try
                {
                    resp.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException e)
                {
                    logger.LogError(e, "[{vendorname}] : Fail to inquire URL content headers\n", vp.Name);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogDebug("[{vn}] : exception: {ex} ", vp.Name, ex.Message);
                if (secondaryClient != null)
                {
                    logger.LogWarning("[{vn}] : HTTP HEAD request timed out with proxy setting in config file, try no proxy setting or default proxy setting now", vp.Name);
                    HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Head, vp.DownloadUri);
                    resp = await secondaryClient.SendAsync(req);
                    try
                    {
                        resp.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException e2)
                    {
                        logger.LogError(e2, "[{vendorname}] : Fail to inquire URL content headers\n", vp.Name);
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            logger.LogDebug("[{vn}] : Received content headers", vp.Name);
            return UpdateContentLength(resp, vp);
        }
    }
}
