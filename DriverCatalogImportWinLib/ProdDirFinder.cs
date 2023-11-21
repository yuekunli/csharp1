using System;
using System.Collections.Generic;
using System.IO;

namespace DriverCatalogImporter
{
    internal class ProdDirFinder : IDirFinder
    {
        private string rootDir;

        private string workingDir;
        public ProdDirFinder(string _rootDir)
        {
            rootDir = _rootDir;
            if (!Directory.Exists(rootDir))
            {
                Directory.CreateDirectory(rootDir);
            }

            workingDir = Path.Combine(_rootDir, "data", "DriverCatalog");
            if (!Directory.Exists(workingDir))
            {
                Directory.CreateDirectory(workingDir);
            }
        }

        public string GetNewCabFileDir()
        {
            string s = Path.Combine(workingDir, "new");
            if (!Directory.Exists(s))
            {
                Directory.CreateDirectory(s);
            }
            return s;
        }

        public string GetOldCabFileDir()
        {
            string s = Path.Combine(rootDir, "old");
            if (!Directory.Exists(s))
            {
                Directory.CreateDirectory(s);
            }
            return s;
        }

        public string GetCabExtractOutputDir()
        {
            return workingDir;
        }

        public string GetLogFileDir()
        {
            string s = Path.Combine(rootDir, "logs");
            if (!Directory.Exists(s))
            {
                Directory.CreateDirectory(s);
            }
            return s;
        }

        public string GetTmpSdpFileDir()
        {
            return workingDir;
        }

        public string GetFlagFileDir()
        {
            string s = Path.Combine(rootDir, "data");
            if (!Directory.Exists(s))
            {
                Directory.CreateDirectory(s);
            }
            return s;
        }

        public string[] GetConfigFileDir()
        {
            List<string> possibleDirs = new List<string>();

            string s = Environment.GetEnvironmentVariable("DirverCatalogImportCfgDir");
            if (s != null && Directory.Exists(s))
            {
                possibleDirs.Add(s);
            }

            s = Path.Combine(rootDir, "config");
            if (Directory.Exists(s))
            {
                possibleDirs.Add(s);
            }
            
            s = @"C:\Temp\";
            if (Directory.Exists(s))
                possibleDirs.Add(s);

            possibleDirs.Add(@"C:\");

            return possibleDirs.ToArray();
        }

        public string GetVendorProfileOverrideFileDir()
        {
            string s = Path.Combine(rootDir, "config");
            if (!Directory.Exists(s))
            {
                Directory.CreateDirectory(s);
            }
            return s;
        }
    }
}
