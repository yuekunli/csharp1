using System;
using System.Collections.Generic;
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
            //WsusImporter.test();
            DriverCatalogImporter.WsusImporterTestWrapper.TestWsusImporter();
        }
    }
}
