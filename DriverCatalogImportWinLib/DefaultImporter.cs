using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    internal class DefaultImporter : IImporter
    {
        private readonly ILogger logger;
        private IDirFinder dirFinder;
        public DefaultImporter(ILogger _logger, IDirFinder _dirFinder)
        {
            logger = _logger;
            this.dirFinder = _dirFinder;
        }
        public ImportStats ImportFromXml(VendorProfile vp, ImportInstructions instruct)
        {
            string xmlFileName = Path.ChangeExtension(vp.CabFileName, ".xml");
            string xmlFilePath = Path.Combine(dirFinder.GetCabExtractOutputDir(), xmlFileName);
            if (File.Exists(xmlFilePath))
            {
                logger.LogInformation("[{VendorName}] : ImportFromXml, XML file exists", vp.Name);
            }
            else
            {
                logger.LogError("[{VendorName}] : ImportFromXml, XML file does not exist", vp.Name);
            }
            return new ImportStats(1, 1, 0);
        }

        public bool ImportFromSdp(string sdpFilePath)
        {
            return true;
        }
    }
}
