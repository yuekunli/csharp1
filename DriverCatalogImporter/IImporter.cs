namespace DriverCatalogImporter
{
    internal interface IImporter
    {
        Task<bool> ImportFromXml(VendorProfile vp);
    }
}
