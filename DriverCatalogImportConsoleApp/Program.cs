using DriverCatalogImporter;
class Program
{
    static void Main(string[] args)
    {
        Task.Run(  () => {
            new ThirdPartyDriverCatalogImporter().Start();
        }).Wait();
    }
}