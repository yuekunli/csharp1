using Microsoft.Extensions.Logging;
using SevenZip;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    internal class CabExtractor
    {
        private ILogger logger;
        private IDirFinder dirFinder;
        public CabExtractor(ILogger _logger, IDirFinder _dirFinder) {
            SevenZipBase.SetLibraryPath("7z.dll");
            this.logger = _logger;
            dirFinder = _dirFinder;
        }

        public async Task<bool> extractXml(VendorProfile vp)
        {
            string cabFilePath = Path.Combine(dirFinder.GetNewCabFileDir(), vp.CabFileName);
            string xmlFileName = Path.ChangeExtension(vp.CabFileName, ".xml");
            string extractedXmlFilePath = Path.Combine(dirFinder.GetCabExtractOutputDir(), xmlFileName);
            try
            {
                using (SevenZipExtractor extractor = new SevenZipExtractor(cabFilePath))
                {
                    using (FileStream fs = File.Create(extractedXmlFilePath))
                    {
                        await extractor.ExtractFileAsync(xmlFileName, fs);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public bool DeleteXml(VendorProfile vp)
        {
            try
            {
                string xmlFileName = Path.ChangeExtension(vp.CabFileName, ".xml");
                string extractedXmlFilePath = Path.Combine(dirFinder.GetCabExtractOutputDir(), xmlFileName);
                if (File.Exists(extractedXmlFilePath))
                {
                    File.Delete(extractedXmlFilePath);
                }
                return true;
            }
            catch(Exception ex)
            {
                return false;
            }
        }
    }
}
