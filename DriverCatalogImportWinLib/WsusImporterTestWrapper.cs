using DriverCatalogImporter;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    public class WsusImporterTestWrapper
    {


        private static VendorProfile[] vendors = new VendorProfile[] {
            new VendorProfile("Dell", @"https://downloads.dell.com/Catalog/DellSDPCatalogPC.cab", true),
            new VendorProfile("Fujitsu", @"https://support.ts.fujitsu.com/GFSMS/globalflash/FJSVUMCatalogForSCCM.cab", true),
            new VendorProfile("HP", @"https://hpia.hpcloud.hp.com/downloads/sccmcatalog/HpCatalogForSms.latest.cab", true),
            new VendorProfile("Lenovo", @"https://download.lenovo.com/luc/v2/LenovoUpdatesCatalog2v2.cab", true),
            new VendorProfile("DellServer", @"https://downloads.dell.com/Catalog/DellSDPCatalog.cab", true),
            new VendorProfile("HPEnterprise", @"https://downloads.hpe.com/pub/softlib/puc/hppuc.cab", true)
        };

        public static void TestWsusImporter()
        {
        
            VendorProfile v = vendors[0];

            IDirFinder dirFinder  = new SimpleDirFinder(@"C:\Temp\");

            string logFilePath = Path.Combine(dirFinder.GetLogFileDir(), "DriverCatalogImport.log");
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new FileLoggerProvider(logFilePath, new FileLoggerOptions()
            {
                FileSizeLimitBytes = 5 * 1024 * 1024,
                MaxRollingFiles = 3,
                Append = true,
                MinLevel = LogLevel.Debug,
                UseUtcTimestamp = true
            }));
            ILogger logger = loggerFactory.CreateLogger<WsusImporterTestWrapper>();
            logger.LogInformation("====================================");

            IImporter imp = new WsusImporter(loggerFactory.CreateLogger<WsusImporter>(), dirFinder);

            var impTask = imp.ImportFromXml(v, new ImportInstructions(true, false));
            if (impTask.Result.Total > 0) // accesssing Result blocks the calling thread, it's equivalent to calling the Wait method
            {
                if (impTask.Result.Total == impTask.Result.Success)
                {
                    logger?.LogInformation("[{VendorName}] : Success, import from XML file", v.Name);
                    v.RunResult = AImporter.RunResult.Success_Imported;
                }
                else if (impTask.Result.Total > impTask.Result.Success && impTask.Result.Success > 0)
                {
                    logger?.LogInformation("[{vn}] : Partial Success, import from XML file", v.Name);
                    v.RunResult = AImporter.RunResult.Partial_Success_Imported;
                }
                else if (impTask.Result.Success == 0)
                {
                    logger?.LogCritical("[{vn}] : All Fail, import from XML file", v.Name);
                    v.RunResult = AImporter.RunResult.All_Fail_Import;
                }
            }
            else
            {
                logger?.LogError("[{VendorName}] : Failure, import XML file", v.Name);
                v.RunResult = AImporter.RunResult.Fail_Parsing_XML;
            }
            logger?.LogInformation("[{vn}] : Import result: Total: {t}, Success: {s}, Fail: {f}", v.Name, impTask.Result.Total, impTask.Result.Success, impTask.Result.Failure);
        }
    }
}
