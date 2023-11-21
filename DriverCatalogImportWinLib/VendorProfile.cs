using System;
using System.IO;

namespace DriverCatalogImporter
{
    internal class VendorProfile
    {
        public string Name { get; set; }
        public Uri DownloadUri { get; set; }
        public string CabFileName { get; set; }
        public string ExtractOutputFolderName { get; set; }
        public bool Eligible { get; set; }
        public bool HasChange { get; set; }

        public VendorProfile(string name, string url, bool eligible)
        {
            Name = name;
            DownloadUri = new Uri(url);
            CabFileName = Path.GetFileName(url);
            ExtractOutputFolderName = Path.GetFileNameWithoutExtension(url);
            Eligible = eligible;
            HasChange = false;
        }
    }
}
