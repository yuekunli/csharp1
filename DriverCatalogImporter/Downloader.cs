using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace DriverCatalogImporter
{
    internal class Downloader
    {
        private readonly HttpClient cl;
        private readonly HttpClient clWithProxy;
        private readonly ILogger logger;

        private readonly IDirFinder dirFinder;
        public Downloader(ILogger _logger, IDirFinder _dirFinder)
        {
            var proxy = new WebProxy
            {
                Address = new Uri(@"http://192.168.50.2:8080")
            };
            var httpClientHandler = new HttpClientHandler
            {
                Proxy = proxy
            };
            clWithProxy = new HttpClient(httpClientHandler);
            
            cl = new HttpClient();
            // HttpClient has a static member "DefaultProxy", cl inherits it.
            // at this point, the DefaultProxy doesn't have an address.
            // When cl actually sends a requests, "DefaultProxy" will read environment variable settings
            
            logger = _logger;
            dirFinder = _dirFinder;
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

        public async Task<bool> DownloadCab(VendorProfile vp)
        {
            HttpResponseMessage? resp = null;
            try
            {
                HttpRequestMessage req = BuildRequest(vp);
                resp = await cl.SendAsync(req);
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
                string? httpsProxy = Environment.GetEnvironmentVariable("https_proxy");
                string? httpProxy = Environment.GetEnvironmentVariable("http_proxy");
                logger.LogDebug("[{vn}] : Default proxy setting: HTTP proxy: {ip1},   HTTPS proxy: {ip2}  ", vp.Name, httpProxy, httpsProxy);
                logger.LogWarning("[{vn}] : Download attempt timed out with no proxy setting or default proxy setting, try with explicit proxy setting now", vp.Name);
                HttpRequestMessage req = BuildRequest(vp); // is there a way I can reset a request message so that I don't have to re-build one?
                resp = await clWithProxy.SendAsync(req);
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
            logger.LogDebug("[{vendorname}] : Downloaded cab file", vp.Name);
            string downloadedCabFilePath = Path.Join(dirFinder.GetNewCabFileDir(), vp.CabFileName);
            using FileStream fs = File.Create(downloadedCabFilePath);
            var t = resp.Content.CopyToAsync(fs);
            await t;
            if (t.IsCompleted)
            {
                logger.LogDebug("[{vendorname}] : Saved cab file", vp.Name);
                resp.Dispose();
                return true;
            }
            else
            {
                logger.LogError("[{vendorname}] : Saved cab file", vp.Name);
                resp.Dispose();
                return false;
            }
        }

        public async Task<bool> CheckCabSize(VendorProfile vp)
        {
            HttpResponseMessage? resp = null;
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Head, vp.DownloadUri);
                resp = await cl.SendAsync(req);
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
                string? httpsProxy = Environment.GetEnvironmentVariable("https_proxy");
                string? httpProxy = Environment.GetEnvironmentVariable("http_proxy");
                logger.LogDebug("[{vn}] : Default proxy setting: HTTP proxy: {ip1},   HTTPS proxy: {ip2}  ", vp.Name, httpProxy, httpsProxy);
                logger.LogWarning("[{vn}] : HTTP HEAD request timed out with no proxy setting or default proxy setting, try with explicit proxy setting now", vp.Name);
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Head, vp.DownloadUri);
                resp = await clWithProxy.SendAsync(req);
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

            logger.LogDebug("[{vn}] : Received content headers", vp.Name);
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
    }
}
