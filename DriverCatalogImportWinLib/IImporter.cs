namespace DriverCatalogImporter
{
    internal interface IImporter
    {
        bool ImportFromXml(VendorProfile vp);

        bool ImportFromSdp(string sdpFilePath);
    }
}
