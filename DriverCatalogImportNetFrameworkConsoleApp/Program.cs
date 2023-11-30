using DriverCatalogImporter;
using System.Threading.Tasks;

namespace DriverCatalogImportNetFrameworkConsoleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Task t = new AImporter(false).RunOnce();
            t.Wait();
        }
    }
}
