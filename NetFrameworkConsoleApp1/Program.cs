using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetFrameworkConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //XmlParser.TryTheParser();
            //CabFileWorker.extract();
            //CabExtractor.ExtractCabFile();
            
            Task.Run(async 
                () => { 
                    await CabExtractor.ExtractCabFile_IterateArchiveToFindXml(); 
                } ).GetAwaiter().GetResult();
            
            //WsusImporter.test();
            //DriverCatalogImporter.WsusImporterTestWrapper.TestWsusImporter();
            //string a = "Hello World";
            //Console.WriteLine(a.IndexOf("hello", StringComparison.InvariantCultureIgnoreCase));
            //Console.WriteLine(a.IndexOf("wORLD", StringComparison.InvariantCultureIgnoreCase));
            //DriverCatalogImporter.WsusImporterTestWrapper.TestWsusImportFromXml();
            //DriverCatalogImporter.WsusImporterTestWrapper.TestWsusImportFromSdp();

            /*
            string xmlFileName = Path.ChangeExtension("HpCatalogForSms.latest.cab", ".xml");
            Console.WriteLine(xmlFileName);
            */
        }
    }
}
