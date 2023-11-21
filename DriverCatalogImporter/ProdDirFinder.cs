namespace DriverCatalogImporter
{
    internal class ProdDirFinder : IDirFinder
    {
        private string rootDir;

        private string dataDir;
        public ProdDirFinder(string _rootDir)
        {
            rootDir = _rootDir;
            if (!Directory.Exists(rootDir))
            {
                throw new Exception("root directory does not exist");
            }

            dataDir = Path.Join(_rootDir, "data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
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
            string s = Path.Join(rootDir, "logs");
            if (!Directory.Exists(s))
            {
                Directory.CreateDirectory(s);
            }
            return s;
        }

        public string GetTmpSdpFileDir()
        {
            return dataDir;
        }

        public string GetFlagFileDir()
        {
            string? s = Environment.GetEnvironmentVariable("adaptivaserver");
            if (Directory.Exists(s))
            {
                s = Path.Join(s, "data", "DriverCatalogs");
                if (!Directory.Exists(s))
                {
                    Directory.CreateDirectory(s);
                }
                return s;
            }
            else if (Directory.Exists(@"C:\Temp\"))
            {
                return @"C:\Temp\";
            }
            else
                return @"C:\";
        }

        public string[] GetConfigFileDir()
        {
            List<string> possibleDirs = new List<string>();

            string? s = Environment.GetEnvironmentVariable("DirverCatalogImportCfgDir");
            if (s != null && Directory.Exists(s))
            {
                possibleDirs.Add(s);
            }

            possibleDirs.Add(rootDir);

            return possibleDirs.ToArray();
        }

        public string GetVendorProfileOverrideFileDir()
        {
            return rootDir;
        }
    }
}
