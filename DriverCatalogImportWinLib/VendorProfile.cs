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
        public int OldContentLength { get; set; }
        public int NewContentLength { get; set; }
        public DateTime LastDownload { get; set; }
        public AImporter.RunResult RunResult { get; set; } 


        public VendorProfile(string name, string url, bool eligible)
        {
            Name = name;
            DownloadUri = new Uri(url);
            CabFileName = Path.GetFileName(url);
            ExtractOutputFolderName = Path.GetFileNameWithoutExtension(url);
            Eligible = eligible;
            HasChange = false;
            OldContentLength = 0;
            NewContentLength = 0;
            RunResult = AImporter.RunResult.Success_NoChange;
            //LastDownload = null;  // DateTime is a struct, hence a value type, not a reference type, can't assign null to value type without converting
        }

        public void Update(string url, bool elibigle)
        {
            if (DownloadUri.AbsolutePath.Equals(url) && Eligible == elibigle)
            {
                return;
            }
            else if (DownloadUri.AbsolutePath.Equals(url) && Eligible != elibigle)
            {
                Eligible = elibigle;
                return;
            }
            else // !DownloadUri.AbsolutePath.Equals(url))
            {
                DownloadUri = new Uri(url);
                Eligible = elibigle;
                CabFileName = Path.GetFileName(url);
                ExtractOutputFolderName = Path.GetFileNameWithoutExtension(url);
                HasChange = false;
                OldContentLength = 0;
                RunResult = AImporter.RunResult.Success_NoChange;
                //LastDownload = null;
            }
        }
    }
}
