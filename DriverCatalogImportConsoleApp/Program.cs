using DriverCatalogImporter;
class Program
{
    static void Main(string[] args)
    {
        new ThirdPartyDriverCatalogImporter(false).Start();
    }
}