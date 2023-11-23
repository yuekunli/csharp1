using DriverCatalogImporter;

namespace DriverCatalogImportNetFrameworkConsoleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            new AImporter(false).RunOnce();
        }
    }
}
