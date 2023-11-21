using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    internal class SimpleDirFinder : IDirFinder
    {
        private string rootDir;

        private string workingDir;

        public SimpleDirFinder(string _rootDir)
        {
            rootDir = _rootDir;
            if (!Directory.Exists(rootDir))
            {
                Directory.CreateDirectory(rootDir);
            }
            workingDir = Path.Join(_rootDir, "DriverCatalog");
            if (!Directory.Exists(workingDir))
            {
                Directory.CreateDirectory(workingDir);
            }
        }

        public string GetNewCabFileDir()
        {
            string s = Path.Join(workingDir, "new");
            if (!Directory.Exists(s))
            {
                Directory.CreateDirectory(s);
            }
            return s;
        }

        public string GetOldCabFileDir()
        {
            string s = Path.Join(workingDir, "old");
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
            return rootDir;
        }

        public string GetTmpSdpFileDir()
        {
            return workingDir;
        }

        public string GetFlagFileDir()
        {
            return rootDir;
        }

        public string[] GetConfigFileDir()
        {
            List<string> possibleDirs = new List<string>();

            string? s = Environment.GetEnvironmentVariable("DirverCatalogImportCfgDir");
            if (s != null && Directory.Exists(s))
            {
                possibleDirs.Add(s);
            }

            s = Path.Join(rootDir, "config");
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
            return rootDir;
        }
    }
}
