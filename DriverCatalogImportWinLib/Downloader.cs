using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    internal class Downloader
    {
        private HttpClient cl;

        private readonly ILogger logger;

        private IDirFinder dirFinder;
        public Downloader(ILogger _logger, IDirFinder _dirFinder)
        {
            cl = new HttpClient();
            logger = _logger;
            dirFinder = _dirFinder;
        }

        public async Task<bool> downloadCab(VendorProfile vp)
        {
            if (cl == null)
            {
                return false;
            }
            HttpResponseMessage resp = await cl.GetAsync(vp.DownloadUri);
            try
            {
                resp.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                resp.Dispose();
                return false;
            }

            string downloadedCabFilePath = Path.Combine(dirFinder.GetNewCabFileDir(), vp.CabFileName);
            FileStream fs = File.Create(downloadedCabFilePath);
            var t = resp.Content.CopyToAsync(fs);
            await t;
            fs.Close();
            if (t.IsCompleted)
                return true;
            else
                return false;
        }
    }
}
