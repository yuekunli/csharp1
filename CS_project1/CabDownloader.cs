using System.Net;
using System.Net.Http.Headers;

namespace CS_project1
{
    internal class CabDownloader
    {
        public CabDownloader() { }

        static public void downloadDell()
        {
            string dellURL = @"https://downloads.dell.com/Catalog/DellSDPCatalogPC.cab";
            string saveAs = @"C:\\Users\\YuekunLi\\Downloads\\b\\DellSDPCatalogPC.cab";

            WebClient wc = new WebClient();
            wc.Headers.Add(HttpRequestHeader.UserAgent, "Other");
            wc.Headers.Add(HttpRequestHeader.Accept, @"text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            wc.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate, br");
            wc.DownloadFile(dellURL, saveAs);
            Console.WriteLine("download done");
        }

        static public void CheckHeader()
        {
            HttpClient cl = new HttpClient();
            //Uri uri = new Uri(@"https://downloads.dell.com/Catalog/DellSDPCatalogPC.cab");
            Uri uri = new Uri(@"https://downloads.hpe.com/pub/softlib/puc/hppuc.cab");
            var resp = cl.Send(new HttpRequestMessage(HttpMethod.Head, uri));
            //var resp = cl.Send(new HttpRequestMessage(HttpMethod.Get, uri));
            try
            {
                resp.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                
                return;
            }
            HttpContentHeaders h = resp.Content.Headers;
            IEnumerable<string> length = h.GetValues("Content-Length");
            string[] a = Enumerable.ToArray<string>(length);

            Console.WriteLine("content-length: {0} ", a[0]);
        }
    }
}
