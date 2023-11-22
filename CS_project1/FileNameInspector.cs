using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace CS_project1
{
    internal class FileNameInspector
    {
        public static void CheckFileName()
        {
            string url = @"https://hpia.hpcloud.hp.com/downloads/sccmcatalog/HpCatalogForSms.latest.cab";

            Console.WriteLine(Path.GetFileName(url));
            Console.WriteLine(Path.GetFileNameWithoutExtension(url));
            Console.WriteLine(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(url)));
            string a = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(url));
            Console.WriteLine(Path.ChangeExtension(a, ".xml"));
        }

        public static void CheckIP()
        {
            string address = "192.168.50.2:5050";

            Uri uri = new Uri(String.Format("http://{0}", address));

            Console.WriteLine(uri.ToString());
            Console.WriteLine(uri.Host);
            Console.WriteLine(uri.Port);
            IPAddress ipadd = IPAddress.Parse(uri.Host);
            IPEndPoint p1 = IPEndPoint.Parse(address);
            Console.WriteLine(p1.Address.ToString());
            Console.WriteLine(p1.Port);

            string address2 = "127.16.16.61";
            IPEndPoint p2 = IPEndPoint.Parse(address2);
            Console.WriteLine(p2.Address.ToString());
            Console.WriteLine(p2.Port);

            Uri url;
            IPAddress ip;
            if (Uri.TryCreate(String.Format("http://{0}", "127.0.0.1:5"), UriKind.Absolute, out url) &&
               IPAddress.TryParse(url.Host, out ip))
            {
                IPEndPoint endPoint = new IPEndPoint(ip, url.Port);
            }

        }
    }
}
