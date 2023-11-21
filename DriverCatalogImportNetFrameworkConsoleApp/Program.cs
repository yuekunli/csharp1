using System.Threading;
using DriverCatalogImporter;

namespace DriverCatalogImportNetFrameworkConsoleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            new ThirdPartyDriverCatalogImporter().Start();
            //Thread.Sleep(60 * 60 * 1000);
        }
    }
}
