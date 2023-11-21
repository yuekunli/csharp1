namespace DriverCatalogImporter
{
    internal class ServerDirFinder
    {
        private string rootDir;

        private string dataDir;

        private string logDir;

        private string cfgDir;
        public ServerDirFinder()
        {
            rootDir = Environment.GetEnvironmentVariable("adaptivaserver");

            if (!Directory.Exists(rootDir))
            {
                throw new Exception("root directory does not exist");
            }

            dataDir = Path.Join(rootDir, "data", "DriverCatalogs");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            logDir = Path.Join(rootDir, "logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            cfgDir = Path.Join(rootDir, "config");
            if (!Directory.Exists(cfgDir))
            {
                Directory.CreateDirectory(cfgDir);
            }
        }

        public string GetNewCabFileDir()
        {
            string s = Path.Join(dataDir, "new");
            if (!Directory.Exists(s))
            {
                Directory.CreateDirectory(s);
            }
            return s;
        }

        public string GetOldCabFileDir()
        {
            string s = Path.Join(dataDir, "old");
            if (!Directory.Exists(s))
            {
                Directory.CreateDirectory(s);
            }
            return s;
        }

        public string GetCabExtractOutputDir()
        {
            return dataDir;
        }

        public string GetLogFileDir()
        {
            return logDir;
        }

        public string GetTmpSdpFileDir()
        {
            return dataDir;
        }

        public string GetFlagFileDir()
        {
            return dataDir;
        }

        public string[] GetConfigFileDir()
        {
            List<string> possibleDirs = new List<string>();

            possibleDirs.Add(cfgDir);

            return possibleDirs.ToArray();
        }

        public string GetVendorProfileOverrideFileDir()
        {
            return cfgDir;
        }
    }
}
