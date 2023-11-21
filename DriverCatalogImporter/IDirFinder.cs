namespace DriverCatalogImporter
{
    internal interface IDirFinder
    {
        public string GetNewCabFileDir();

        public string GetOldCabFileDir();

        public string GetCabExtractOutputDir();

        public string GetLogFileDir();

        public string GetTmpSdpFileDir();

        public string GetFlagFileDir();

        public string[] GetConfigFileDir();
        public string GetVendorProfileOverrideFileDir();
    }
}
