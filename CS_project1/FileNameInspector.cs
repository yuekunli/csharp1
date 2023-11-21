using System;
using System.Collections.Generic;
using System.Linq;
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


    }
}
