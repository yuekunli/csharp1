using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using System;
using System.IO;
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

        public static void TestWsusImportFromXml()
        {
            VendorProfile v = vendors[3];

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

            var impResult = imp.ImportFromXml(v, new ImportInstructions(/*async each pkg*/true, /*update db*/false, /*async update db*/false, /*publish or delete*/false, /*marshal detectoid*/false, /*only 1 detect in lenovo*/true));
            if (impResult.Total > 0) // accesssing Result blocks the calling thread, it's equivalent to calling the Wait method
            {
                if (impResult.Total == impResult.Success)
                {
                    logger?.LogInformation("[{VendorName}] : Success, import from XML file", v.Name);
                    v.RunResult = AImporter.RunResult.Success_Imported;
                }
                else if (impResult.Total > impResult.Success && impResult.Success > 0)
                {
                    logger?.LogInformation("[{vn}] : Partial Success, import from XML file", v.Name);
                    v.RunResult = AImporter.RunResult.Partial_Success_Imported;
                }
                else if (impResult.Success == 0)
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
            logger?.LogInformation("[{vn}] : Import result: Total: {t}, Success: {s}, Fail: {f}", v.Name, impResult.Total, impResult.Success, impResult.Failure);
        }


        public static void TestWsusImportFromSdp()
        {
            /*
            string[] sdpFiles = { @"C:\Temp\DriverCatalog\0001dbe9-c5bd-4487-8f1a-c2d9a8808c5b.sdp",
            @"C:\Temp\DriverCatalog\2d2bff76-ea51-4408-b4e6-abf4b1d1e291.sdp",
            @"C:\Temp\DriverCatalog\0010f178-d87c-4185-81f5-1331e8e776e7.sdp",
            @"C:\Temp\DriverCatalog\000056ef-51e5-4d8a-be27-0ef247a591aa.sdp",
            @"C:\Temp\DriverCatalog\ef78bc5c-d587-4b1e-afb3-a1d50a9eb886.sdp"};
            */
            string[] sdpFiles = { @"C:\Temp\DriverCatalog\f4899c93-108b-413b-97c1-1b4f5b0d39d7.xml" };
            VendorProfile v = vendors[4];

            IDirFinder dirFinder = new SimpleDirFinder(@"C:\Temp\");

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

            WsusImporter imp = new WsusImporter(loggerFactory.CreateLogger<WsusImporter>(), dirFinder);
            foreach(string f in sdpFiles)
            {
                try
                {
                    Task<bool> t = imp.ImportFromSingleSdp(f, /*publish or delete*/true, new SinglePackagePublishInstruction(v, false, false, f, true, false, false, null));
                    t.Wait();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "[{vn}] : Fail\n", v.Name);
                }
            }
        }
    }
}
