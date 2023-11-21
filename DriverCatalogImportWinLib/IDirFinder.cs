namespace DriverCatalogImporter
{
    internal interface IDirFinder
    {
        string GetNewCabFileDir();

        string GetOldCabFileDir();

        string GetCabExtractOutputDir();

        string GetLogFileDir();

        string GetTmpSdpFileDir();

        string GetFlagFileDir();

        string[] GetConfigFileDir();
        string GetVendorProfileOverrideFileDir();
    }
}
