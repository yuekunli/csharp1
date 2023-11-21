using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using System.Globalization;
using System.Timers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DriverCatalogImporter
{

    /*
     * Directory Schema:
     *
     *
     * %adaptivaserver%
     *   |
     *   +-- data
     *   |    |
     *   |    +-- DriverCatalog   (workingDir)
     *   |         |
     *   |         +-- old
     *   |         |   |
     *   |         |   +-- DellSDPCatalogPC.cab
     *   |         |   +-- FJSVUMCatalogForSCCM.cab
     *   |         |   +-- ...
     *   |         |
     *   |         +-- new
     *   |         |   |
     *   |         |   +-- DellSCPCatalogPC.cab
     *   |         |   +-- FJSVUMCatalogForSCCM.cab
     *   |         |   +-- ...
     *   |         |
     *   |         +-- DellSDPCatalogPC.xml  (temporary)
     *   |         |
     *   |         +-- abcd1234abcdefef5678.sdp  (temporary)
     *   |
     *   +-- logs
     *   |   |
     *   |   +-- DriverCatalogImport.log
     *   |
     *   +-- config
     *       |
     *       +-- DriverCatalogImportCfg.txt
     *       |
     *       +-- VendorProfileOverride.txt
     *
     *
     * C:\Temp\
     *   |
     *   +-- DriverCatalog   (workingDir)
     *   |     |
     *   |     +-- old
     *   |     |   |
     *   |     |   +-- DellSDPCatalogPC.cab
     *   |     |   +-- FJSVUMCatalogForSCCM.cab
     *   |     |   +-- ...
     *   |     |
     *   |     +-- new
     *   |     |   |
     *   |     |   +-- DellSCPCatalogPC.cab
     *   |     |   +-- FJSVUMCatalogForSCCM.cab
     *   |     |   +-- ...
     *   |     |
     *   |     +-- DellSDPCatalogPC.xml  (temporary)
     *   |     |
     *   |     +-- abcd1234abcdefef5678.sdp  (temporary)
     *   |
     *   |
     *   +-- DriverCatalogImport.log
     *   |
     *   +-- DriverCatalogImportCfg.txt
     *   |
     *   +-- VendorProfileOverride.txt
     * 
     */

    public class ThirdPartyDriverCatalogImporter : IDisposable
    {
        private enum RunResult
        {
            Success_NoChange = 0,
            Success_ImportDone,
            Fail_Download,
            Fail_ExtractXml,
            Fail_ExtractSdp,
            Fail_Compare,
            Fail_Import
        }

        private static VendorProfile[] vendors = new VendorProfile[] {
            new VendorProfile("Dell", @"https://downloads.dell.com/Catalog/DellSDPCatalogPC.cab", true),
            new VendorProfile("Fujitsu", @"https://support.ts.fujitsu.com/GFSMS/globalflash/FJSVUMCatalogForSCCM.cab", true),
            new VendorProfile("HP", @"https://hpia.hpcloud.hp.com/downloads/sccmcatalog/HpCatalogForSms.latest.cab", true),
            new VendorProfile("Lenovo", @"https://download.lenovo.com/luc/v2/LenovoUpdatesCatalog2v2.cab", true),
            new VendorProfile("DellServer", @"https://downloads.dell.com/Catalog/DellSDPCatalog.cab", false)
        };

        private readonly System.Timers.Timer aTimer;

        private readonly Downloader dl;

        private readonly CabExtractor ce;

        private readonly FileComparer fc;

        private readonly IImporter imp;

        private ILoggerFactory loggerFactory;

        private ILogger logger = null;

        private IDirFinder dirFinder;

        // configuration options:

        private readonly bool isProd;
        private string vendorProfileOverrideFilePath = null;
        private bool shouldParseConfigFileOnEveryRun = true;
        private bool printVendorProfileOnEveryRun = true;
        private bool shouldParseVendorProfileOverrideFileOnEveryRun = true;
        private int runIntervalInSeconds = 60 * 60;
        private int runTimeoutInSeconds = 5 * 60;
        private bool shouldUseWsusImport = true;
        private LogLevel minLogLevel = LogLevel.Information;

        public ThirdPartyDriverCatalogImporter(bool prod)
        {
            isProd = prod;
            
            if (prod)
                SetupDirFinder();
            else
                dirFinder = new SimpleDirFinder(@"C:\Temp\");

            if (dirFinder == null)
            {
                throw new Exception("Fail to create directory finder");
            }

            ParseConfigFile();

            CreateLogger(1);

            ParseVendorProfileOverrideFile();

            if (loggerFactory == null || logger == null)
            {
                throw new Exception("Fail to create logger factory or logger");
            }

            logger.LogDebug("Initialize ThirdPartyDriverCatalogImporter, prod: {isProd}", isProd);

            dl = new Downloader(loggerFactory.CreateLogger<Downloader>(), dirFinder);
            if (dl == null)
            {
                logger.LogError("Fail to create downloader");
                throw new Exception("Fail to create downloader");
            }
            ce = new CabExtractor(loggerFactory.CreateLogger<CabExtractor>(), dirFinder);
            if (ce == null)
            {
                logger.LogError("Fail to create cab extractor");
                throw new Exception("Fail to create cab extractor");
            }
            fc = new FileComparer(loggerFactory.CreateLogger<FileComparer>(), dirFinder);
            if (fc == null)
            {
                logger.LogError("Fail to create file comparer");
                throw new Exception("Fail to file comparer");
            }
            if (shouldUseWsusImport)
                imp = new WsusImporter(loggerFactory.CreateLogger<WsusImporter>(), dirFinder);
            else
                imp = new DefaultImporter(loggerFactory.CreateLogger<DefaultImporter>(), dirFinder);

            if (imp == null)
            {
                logger.LogError("Fail to create importer");
                throw new Exception("Fail to create importer");
            }

            aTimer = new System.Timers.Timer();
            if (aTimer == null)
            {
                throw new Exception("Fail to create timer");
            }
        }

        public ThirdPartyDriverCatalogImporter() : this(false)
        {
        }

        private void PrintVendorProfile()
        {
            int longestUrlLength = 0;
            Array.ForEach(vendors, delegate (VendorProfile p)
            {
                if (p.DownloadUri.AbsoluteUri.Length > longestUrlLength)
                    longestUrlLength = p.DownloadUri.AbsoluteUri.Length;
            });

            int fieldWidth = longestUrlLength + 3;
            string formatS = "{0,-15}  {1,-" + fieldWidth + "}  {2,-15}";
            logger.LogInformation("Current Vendor Profile:");
            logger.LogInformation(formatS, "Name", "URL", "Eligible");
            foreach (VendorProfile v in vendors)
            {
                logger.LogInformation(formatS, v.Name, v.DownloadUri.AbsoluteUri, v.Eligible);
            }
        }

        private void ParseConfigOptions(string k, string v)
        {
            if (k.Equals("VendorProfileFilePath", StringComparison.CurrentCultureIgnoreCase))
            {
                if (Path.IsPathRooted(v) && File.Exists(v))
                {
                    vendorProfileOverrideFilePath = v;
                }
                else
                {
                    logger.LogDebug("Invalid vendor profile override file path: {0}", v);
                }
            }
            else if (k.Equals("ShouldParseConfigFileOnEveryRun", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    shouldParseConfigFileOnEveryRun = bool.Parse(v);
                }
                catch (FormatException e)
                {

                }
            }
            else if (k.Equals("PrintVendorProfileOnEveryRun", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    printVendorProfileOnEveryRun = bool.Parse(v);
                }
                catch (FormatException e)
                {

                }
            }
            else if (k.Equals("ShouldUseWsusImport", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    shouldUseWsusImport = bool.Parse(v);
                }
                catch (FormatException e)
                {

                }
            }
            else if (k.Equals("ShouldParseVendorProfileOverrideFileOnEveryRun", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    shouldParseVendorProfileOverrideFileOnEveryRun = bool.Parse(v);
                }
                catch (FormatException e)
                {

                }
            }    
            else if (k.Equals("RunIntervalInSeconds", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    runIntervalInSeconds = int.Parse(v, NumberStyles.Integer | NumberStyles.AllowThousands, new CultureInfo("en-US"));
                }
                catch (Exception e)
                {

                }

            }
            else if (k.Equals("RunTimeoutInSeconds", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    runTimeoutInSeconds = int.Parse(v, NumberStyles.Integer | NumberStyles.AllowThousands, new CultureInfo("en-US"));
                }
                catch (Exception e)
                {

                }
            }
            else if (k.Equals("MinLogLevel", StringComparison.CurrentCultureIgnoreCase))
            {
                if (v.Equals("Trace", StringComparison.CurrentCultureIgnoreCase))
                {
                    minLogLevel = LogLevel.Trace;
                }
                else if (v.Equals("Debug", StringComparison.CurrentCultureIgnoreCase))
                {
                    minLogLevel = LogLevel.Debug;
                }
                else if (v.Equals("Information", StringComparison.CurrentCultureIgnoreCase))
                {
                    minLogLevel = LogLevel.Information;
                }
                else if (v.Equals("Warning", StringComparison.CurrentCultureIgnoreCase))
                {
                    minLogLevel = LogLevel.Warning;
                }
                else if (v.Equals("Error", StringComparison.CurrentCultureIgnoreCase))
                {
                    minLogLevel = LogLevel.Error;
                }
                else if (v.Equals("Critical", StringComparison.CurrentCultureIgnoreCase))
                {
                    minLogLevel = LogLevel.Critical;
                }
                else if (v.Equals("None", StringComparison.CurrentCultureIgnoreCase))
                {
                    minLogLevel = LogLevel.None;
                }
            }
            else
            {
                if (logger != null)
                    logger.LogDebug("Unknown option: {0}", k);
            }
        }

        private void ParseConfigFile()
        {
            string[] possibleConfigFileDirs = dirFinder.GetConfigFileDir();
            string cfgFilePath = null;
            bool found = false;
            vendorProfileOverrideFilePath = null;
            foreach (string s in possibleConfigFileDirs)
            {
                cfgFilePath = Path.Combine(s, "DriverCatalogImportCfg.txt");
                if (File.Exists(cfgFilePath))
                {
                    found = true;
                    break;
                }
            }
            if (found)
            {
                var entries = File.ReadAllLines(cfgFilePath);
                foreach (var entry in entries)
                {
                    var parts = entry.Split('\t');
                    if (parts.Length == 2)
                    {
                        string k = parts[0];
                        string v = parts[1];
                        ParseConfigOptions(k, v);
                    }
                    else if (String.IsNullOrWhiteSpace(entry))
                    {
                        if (logger != null)
                            logger.LogTrace("Empty config entry line");
                    }
                    else
                    {
                        if (logger != null)
                            logger.LogWarning("Invalid config entry: {0}", entry);
                    }
                }
            }
            else
            {
                if (logger != null)
                    logger.LogInformation("No config file, program can still run, all configurable options assume default values");
            }
        }

        private void ParseVendorProfileOverrideFile()
        {
            string filePath = null;
            if (vendorProfileOverrideFilePath != null)
            {
                if (File.Exists(vendorProfileOverrideFilePath))
                {
                    logger.LogInformation("Using vendor profile override file pointed by config entry in config file: {0}", filePath);
                    filePath = vendorProfileOverrideFilePath;
                }
                else
                {
                    logger.LogError("config file provides vendor profile override file, but file does not exist, skip vendor profile overriding, program can still run, using in-memory vendor profile");
                    return;
                }
            }
            else
            {
                filePath = Path.Combine(dirFinder.GetVendorProfileOverrideFileDir(), "VendorProfileOverride.txt");
                if (File.Exists(filePath))
                {
                    logger.LogInformation("Using default vendor profile override file : {0}", filePath);
                }
                else
                {
                    logger.LogInformation("No vendor profile override file, config file does not provide one, none exist at default path, skip vendor profile overriding, program can still run, using in-memory vendor profile");
                    return;
                }
            }
            var entries = File.ReadAllLines(filePath);
            int lineNumber = 1;
            foreach (var entry in entries)
            {
                var parts = entry.Split('\t');
                if (parts.Length == 3)
                {
                    string name = parts[0];
                    string url = parts[1];
                    string eligible = parts[2];
                    for (int i = 0; i < vendors.Length; i++)
                    {
                        if (vendors[i].Name.Equals(name))
                        {
                            vendors[i] = new VendorProfile(name, url, bool.Parse(eligible));
                        }
                    }
                }
                else
                {
                    logger.LogDebug("Invalid vendor profile at line {0}", lineNumber);
                }
                lineNumber++;
            }
        }

        private void CreateLogger(int provider)
        {
            if (provider == 0)
            {
                loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                logger = loggerFactory.CreateLogger<ThirdPartyDriverCatalogImporter>();
            }
            else if (provider == 1)
            {
                string logFilePath = Path.Combine(dirFinder.GetLogFileDir(), "DriverCatalogImport.log");
                loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new FileLoggerProvider(logFilePath, new FileLoggerOptions()
                {
                    FileSizeLimitBytes = 5 * 1024 * 1024,
                    MaxRollingFiles = 3,
                    Append = true,
                    MinLevel = minLogLevel,
                    UseUtcTimestamp = true
                    //FilterLogEntry = (e) =>
                    //{
                    //    return isProd ? e.LogLevel >= LogLevel.Information : e.LogLevel >= LogLevel.Trace;
                    //}
                }));
                logger = loggerFactory.CreateLogger<ThirdPartyDriverCatalogImporter>();
            }
        }

        /**
         * If any steps fails here, let the exception bubble up and let the program terminate.
         * The import can't work without knowing the directory.
         */
        private void SetupDirFinder()
        {
            string installDir = Environment.GetEnvironmentVariable("adaptivaserver");
            if (installDir != null)
            {
                dirFinder = new ProdDirFinder(installDir);
            }
            else
            {
                dirFinder = new ProdDirFinder(@"C:\Temp\");
            }
        }

        /**
         * Technically speaking, the import can still work without a log file.
         */
        private void CheckAndCreateLogFile()
        {
            string logDir = dirFinder.GetLogFileDir();
            string logFilePath = Path.Combine(logDir, "DriverCatalogImport.log");
            if (!File.Exists(logFilePath))
            {
                File.Create(logFilePath).Close();
            }
        }

        private void CleanupAndRenameDir()
        {
            string oldDir = dirFinder.GetOldCabFileDir();
            Directory.Delete(oldDir, true);
            string newDir = dirFinder.GetNewCabFileDir();
            Directory.Move(newDir, oldDir);
        }


        private void WriteFlagFile()
        {
            string flagFilePath = Path.Combine(dirFinder.GetFlagFileDir(), "FLAG.txt");
            if (File.Exists(flagFilePath))
            {
                File.Delete(flagFilePath);
            }
            using (StreamWriter sw = File.CreateText(flagFilePath))
            {
                foreach (VendorProfile v in vendors)
                {
                    if (v.Eligible && v.HasChange)
                    {
                        sw.WriteLine(v.Name);
                    }
                }
            }
        }

        private void onTimeElapsed(object source, ElapsedEventArgs e)
        {
            RunOnce();
        }
        private void RunOnce()
        {
            if (shouldParseConfigFileOnEveryRun)
            {
                ParseConfigFile();
            }
            if (shouldParseVendorProfileOverrideFileOnEveryRun)
            {
                ParseVendorProfileOverrideFile();
            }
            if (printVendorProfileOnEveryRun)
            {
                PrintVendorProfile();
            }

            List<Task> tasks = new List<Task>();

            foreach (VendorProfile v in vendors)
            {
                if (v.Eligible)
                {
                    logger.LogInformation("[{VendorName}] : Eligible, Start task", v.Name);
                    tasks.Add(
                        Task.Run(async () =>
                        {
                            Task<bool> dlTask = dl.downloadCab(v);
                            await dlTask;
                            if (!dlTask.Result)
                            {
                                logger.LogError("[{VendorName}] : Failure, download", v.Name);
                                return;
                            }
                            bool fcResult = fc.IsSame(v);
                            if (fcResult)
                            {
                                logger.LogInformation("[{VendorName}] : Cab files are the same, no action", v.Name);
                                return;
                            }
                            v.HasChange = true;
                            Task<bool> ceTask = ce.extractXml(v);
                            await ceTask;
                            if (!ceTask.Result)
                            {
                                logger.LogError("[{VendorName}] : Failure, extract cab file", v.Name);
                                return;
                            }
                            bool impResult = imp.ImportFromXml(v);
                            if (impResult)
                            {
                                logger.LogInformation("[{VendorName}] : Success, import XML file", v.Name);
                            }
                            else
                            {
                                logger.LogError("[{VendorName}] : Failure, import XML file", v.Name);
                            }
                            bool r = ce.DeleteXml(v);
                            logger.LogInformation("[{VendorName}] : Deleted temporary XML file", v.Name);
                        })
                    );
                }
                else
                {
                    logger.LogInformation("[{VendorName}] : Not eligible, skip", v.Name);
                }
            }
            try
            {
                bool result = Task.WaitAll(tasks.ToArray(), runTimeoutInSeconds * 1000);
                if (result)
                {
                    logger.LogInformation("Run finish in time");
                }
                else
                {
                    logger.LogError("Run Error");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Run Error");
            }
            WriteFlagFile();
            CleanupAndRenameDir();
            if (aTimer.Interval != runIntervalInSeconds*1000)
            {
                aTimer.Interval = runIntervalInSeconds*1000;
                logger.LogInformation("Change timer wakeup interval to {interval} seconds", runIntervalInSeconds);
            }
        }

        public void Start()
        {
            if (aTimer != null)
            {
                aTimer.Elapsed -= onTimeElapsed;
                aTimer.Elapsed += onTimeElapsed;
                aTimer.AutoReset = true;
                aTimer.Interval = runIntervalInSeconds * 1000;

                if (isProd)
                    aTimer.Start();
                else
                    RunOnce();
            }
        }

        public void Stop()
        {
            if (aTimer != null)
            {
                aTimer.Stop();
                aTimer.Dispose();
            }
        }

        public void Dispose()
        {
            loggerFactory.Dispose();
            aTimer?.Dispose();
        }
    }
}
