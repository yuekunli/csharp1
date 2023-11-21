using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using System.Globalization;
using System.Timers;

namespace DriverCatalogImporter
{

    /*
     * Directory Schema:
     *
     *
     * C:\Program Files\Adaptiva\DriverCatalogImport\
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
     *       |
     *       +-- DriverCatalogImport.log
     * 
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
     * 
     */

    public class ThirdPartyDriverCatalogImporter : IDisposable
    {
        public enum RunResult
        {
            Success_NoChange = 0,
            Success_Imported,
            Fail_CheckContentLength,
            Fail_Download,
            Fail_ExtractXml,
            Fail_ExtractSdp,
            Fail_Compare,
            Fail_Import
        }

        private enum LogProvider
        {
            Console = 0,
            File
        }

        private static VendorProfile[] InitialProfiles = new VendorProfile[] {
            new VendorProfile("Dell", @"https://downloads.dell.com/Catalog/DellSDPCatalogPC.cab", true),
            new VendorProfile("Fujitsu", @"https://support.ts.fujitsu.com/GFSMS/globalflash/FJSVUMCatalogForSCCM.cab", false),
            new VendorProfile("HP", @"https://hpia.hpcloud.hp.com/downloads/sccmcatalog/HpCatalogForSms.latest.cab", false),
            new VendorProfile("Lenovo", @"https://download.lenovo.com/luc/v2/LenovoUpdatesCatalog2v2.cab", false),
            new VendorProfile("DellServer", @"https://downloads.dell.com/Catalog/DellSDPCatalog.cab", false),
            new VendorProfile("HPEnterprise", @"https://downloads.hpe.com/pub/softlib/puc/hppuc.cab", false)
        };

        private readonly System.Timers.Timer aTimer;

        private readonly Downloader dl;

        private readonly CabExtractor ce;

        private readonly FileComparer fc;

        private readonly IImporter imp;

        private ILoggerFactory loggerFactory;

        private ILogger? logger;

        private IDirFinder dirFinder;

        private List<VendorProfile> vendors = new List<VendorProfile>();

        private readonly bool isProd;

        private DateTime? lastDownload = null;

        // configuration options:

        // options about config file:
        private bool shouldParseConfigFileOnEveryRun = true;

        // options about vendor profile override
        private string? vendorProfileOverrideFilePath = null;
        private bool shouldParseVendorProfileOverrideOnEveryRun = false;
        private bool vendorProfileOverrideAdditiveOnly = true;

        // options about verboseness
        private bool printVendorProfileInLogOnEveryRun = false;
        private bool dumpVendorProfileAfterRun = false;

        // options about scheduling:
        private bool immediateRunAfterStart = true;
        private int runIntervalInSeconds = 3 * 60;
        private int runTimeoutInSeconds = 1 * 60;

        // options about logging:
        private LogLevel minLogLevel = LogLevel.Information;

        // options about WSUS
        private bool shouldUseWsusImport = true;

        public ThirdPartyDriverCatalogImporter(bool prod)
        {
            isProd = prod;
            
            if (prod)
                dirFinder = new ProdDirFinder(AppContext.BaseDirectory);
            else
                dirFinder = new SimpleDirFinder(@"C:\Temp\");

            if (dirFinder == null)
            {
                throw new Exception("Fail to create directory finder");
            }

            ParseConfigFile();

            CreateLogger(LogProvider.File);

            InitializeVendors();

            if (loggerFactory == null || logger == null)
            {
                throw new Exception("Fail to create logger factory or logger");
            }

            logger?.LogDebug("Initialize ThirdPartyDriverCatalogImporter, prod: {isProd}", isProd);

            try
            {
                dl = new Downloader(loggerFactory.CreateLogger<Downloader>(), dirFinder);
            }
            catch (Exception e)
            {
                logger?.LogError(e, "Fail to create downloader");
                throw new Exception("Fail to create downloader");

            }

            try
            {
                ce = new CabExtractor(loggerFactory.CreateLogger<CabExtractor>(), dirFinder);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Fail to create cab extractor");
                throw new Exception("Fail to create cab extractor");
            }

            try
            {
                fc = new FileComparer(loggerFactory.CreateLogger<FileComparer>(), dirFinder);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Fail to create file comparer");
                throw new Exception("Fail to file comparer");
            }

            try
            {
                if (shouldUseWsusImport)
                    imp = new WsusImporter(loggerFactory.CreateLogger<WsusImporter>(), dirFinder);
                else
                    imp = new DefaultImporter(loggerFactory.CreateLogger<DefaultImporter>(), dirFinder);
            }
            catch (Exception e)
            {
                logger?.LogError(e, "Fail to create importer");
                throw new Exception("Fail to create importer");
            }

            aTimer = new System.Timers.Timer();
        }

        public ThirdPartyDriverCatalogImporter() : this(false)
        {
        }

        private void InitializeVendors()
        {
            Array.ForEach(InitialProfiles, p =>
            {
                vendors.Add(p);
            });
        }

        private string GetVendorProfileFormatString()
        {
            int longestUrlLength = 0;
            vendors.ForEach(p =>
            {
                if (p.DownloadUri.AbsoluteUri.Length > longestUrlLength)
                    longestUrlLength = p.DownloadUri.AbsoluteUri.Length;
            });

            int fieldWidth = longestUrlLength + 3;
            string formatS = "{0,-15}  {1,-" + fieldWidth + "}  {2,-15}";
            return formatS;
        }

        private void PrintVendorProfile()
        {
            string formatS = GetVendorProfileFormatString();
            logger?.LogInformation("Current Vendor Profile:");
            logger?.LogInformation(formatS, "Name", "URL", "Eligible");
            foreach (VendorProfile v in vendors)
            {
                logger?.LogInformation(formatS, v.Name, v.DownloadUri.AbsoluteUri, v.Eligible);
            }
        }

        private void DumpVendorProfile()
        {
            string filePath = Path.Join(dirFinder.GetVendorProfileOverrideFileDir(), "EffectiveVendorProfile.txt");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            string formatS = GetVendorProfileFormatString();
            try
            {
                using (StreamWriter sw = File.CreateText(filePath))
                {
                    foreach (VendorProfile v in vendors)
                    {
                        sw.WriteLine(formatS, v.Name, v.DownloadUri.AbsoluteUri, v.Eligible);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogCritical(ex, "Fail to write flag file");
            }
        }

        private void ParseConfigOptions(string k, string v)
        {
            if (k.Equals("ShouldParseConfigFileOnEveryRun", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    shouldParseConfigFileOnEveryRun = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"ShouldParseConfigFileOnEveryRun\" option, it has to be True or False");
                }
            }
            else if (k.Equals("VendorProfileOverrideFilePath", StringComparison.CurrentCultureIgnoreCase))
            {
                if (Path.IsPathRooted(v) && File.Exists(v))
                {
                    vendorProfileOverrideFilePath = v;
                }
                else
                {
                    logger?.LogDebug("Invalid vendor profile override file path: {0}", v);
                }
            }
            else if (k.Equals("ShouldParseVendorProfileOverrideOnEveryRun", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    shouldParseVendorProfileOverrideOnEveryRun = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"ShouldParseVendorProfileOverrideOnEveryRun\" option, it has to be True or False");
                }
            }
            else if (k.Equals("VendorProfileOverrideAdditiveOnly", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    vendorProfileOverrideAdditiveOnly = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"VendorProfileOverrideAdditiveOnly\" option, it has to be True or False");
                }
            }
            else if (k.Equals("PrintVendorProfileInLogOnEveryRun", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    printVendorProfileInLogOnEveryRun = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"PrintVendorProfileInLogOnEveryRun\" option, it has to be True or False");
                }
            }
            else if (k.Equals("DumpVendorProfileAfterRun", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    dumpVendorProfileAfterRun = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"DumpVendorProfileAfterRun\" option, it has to be True or False");
                }
            }
            else if (k.Equals("ImmediateRunAfterStart", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    immediateRunAfterStart = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"ImmediateRunAfterStart\" option, it has to be True or False");
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
                    logger?.LogError(e, "Invalid value for \"RunIntervalInSeconds\" option, it has to be an integer");
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
                    logger?.LogError(e, "Invalid value for \"RunTimeoutInSeconds\" option, it has to be an integer");
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
                else
                {
                    logger?.LogError("Invalid value for \"MinLogLevel\" option, it has to be one of the possibilities: Trace, Debug, Information, Warning, Error, Critical, None");
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
                    logger?.LogError(e, "Invalid value for \"ShouldUseWsusImport\" option, it has to be True or False");
                }
            }
            else
            {
                logger?.LogDebug("Unknown option: {0}", k);
            }
        }

        private void ParseConfigFile()
        {
            string[] possibleConfigFileDirs = dirFinder.GetConfigFileDir();
            string cfgFilePath = "";
            bool found = false;
            vendorProfileOverrideFilePath = null;
            foreach (string s in possibleConfigFileDirs)
            {
                cfgFilePath = Path.Join(s, "DriverCatalogImportCfg.txt");
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
                        logger?.LogTrace("Empty config entry line");
                    }
                    else
                    {
                        logger?.LogWarning("Invalid config entry: {entry}", entry);
                    }
                }
            }
            else
            {
                logger?.LogDebug("No config file, program can still run, all configurable options assume default values");
            }
        }

        private void ParseVendorProfileOverrideFile()
        {
            string filePath = "";
            if (vendorProfileOverrideFilePath != null)
            {
                if (File.Exists(vendorProfileOverrideFilePath))
                {
                    logger?.LogInformation("Using vendor profile override file provided by config file. Override file: {path}", filePath);
                    filePath = vendorProfileOverrideFilePath;
                }
                else
                {
                    logger?.LogWarning("config file provides vendor profile override file, but file does not exist, skip vendor profile overriding, program can still run, using in-memory vendor profile");
                    return;
                }
            }
            else
            {
                filePath = Path.Join(dirFinder.GetVendorProfileOverrideFileDir(), "VendorProfileOverride.txt");
                if (File.Exists(filePath))
                {
                    logger?.LogInformation("Using default vendor profile override file: {path}", filePath);
                }
                else
                {
                    logger?.LogInformation("No vendor profile override file, config file does not provide one, none exists at default path, skip vendor profile overriding, program can still run, using in-memory vendor profile");
                    return;
                }
            }
            if (!vendorProfileOverrideAdditiveOnly)
            {
                logger?.LogWarning("Vendor profile override in total replacing mode, in-memory records will be cleared");
                vendors.Clear();
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
                    string eligibleS = parts[2];
                    bool eligible = false;
                    try
                    {
                        eligible = bool.Parse(eligibleS);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Wrong syntax in the eligible field for vendor profile at line {line}", lineNumber);
                        continue;
                    }

                    bool found = false;
                    if (vendorProfileOverrideAdditiveOnly)
                    {
                        for (int i = 0; i < vendors.Count; i++)
                        {
                            if (vendors[i].Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                            {
                                vendors[i].Update(url, eligible);
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found)
                    {
                        vendors.Add(new VendorProfile(name, url, eligible));
                        logger?.LogInformation("Add new vendor profile, name: {name}", name);
                    }
                }
                else
                {
                    logger?.LogDebug("Invalid vendor profile at line {linenumber}, each line must have 3 fields", lineNumber);
                }
                lineNumber++;
            }
        }

        private void CreateLogger(LogProvider p)
        {
            if (p == LogProvider.Console)
            {
                loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                logger = loggerFactory.CreateLogger<ThirdPartyDriverCatalogImporter>();
            }
            else if (p == LogProvider.File)
            {
                string logFilePath = Path.Join(dirFinder.GetLogFileDir(), "DriverCatalogImport.log");
                loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new FileLoggerProvider(logFilePath, new FileLoggerOptions()
                {
                    FileSizeLimitBytes = 5 * 1024 * 1024,
                    MaxRollingFiles = 3,
                    Append = true,
                    MinLevel = minLogLevel,
                    UseUtcTimestamp = true
                }));
                logger = loggerFactory.CreateLogger<ThirdPartyDriverCatalogImporter>();
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
            string flagFilePath = Path.Join(dirFinder.GetFlagFileDir(), "FLAG.txt");
            if (File.Exists(flagFilePath))
            {
                File.Delete(flagFilePath);
            }
            try
            {
                using (StreamWriter sw = File.CreateText(flagFilePath))
                {
                    foreach (VendorProfile v in vendors)
                    {
                        if (v.Eligible && v.HasChange && v.RunResult == RunResult.Success_Imported)
                        {
                            sw.WriteLine(v.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogCritical(ex, "Fail to write flag file");
            }
        }

        private void onTimeElapsed(object? source, ElapsedEventArgs e)
        {
            RunOnce();
        }
        public void RunOnce()
        {
            logger.LogInformation("==============================================");
            if (shouldParseConfigFileOnEveryRun)
            {
                ParseConfigFile();
            }
            if (shouldParseVendorProfileOverrideOnEveryRun)
            {
                ParseVendorProfileOverrideFile();
            }
            if (printVendorProfileInLogOnEveryRun)
            {
                PrintVendorProfile();
            }

            List<Task> tasks = new List<Task>();

            foreach (VendorProfile v in vendors)
            {
                if (v.Eligible)
                {
                    logger?.LogInformation("[{VendorName}] : Eligible, Start task", v.Name);
                    tasks.Add(
                        Task.Run(async () =>
                        {
                            TimeSpan? interval = DateTime.Now - v.LastDownload;
                            if (interval?.TotalHours < 24)
                            {
                                Task<bool> headerTask = dl.CheckCabSize(v);
                                await headerTask;
                                if (headerTask.Result)
                                {
                                    if (v.OldContentLength == v.NewContentLength)
                                    {
                                        logger?.LogInformation("[{vn}] : Content-Length remains the same", v.Name);
                                        v.NewContentLength = 0;
                                        v.RunResult = RunResult.Success_NoChange;
                                        return;
                                    }
                                }
                                else
                                {
                                    logger?.LogError("[{vn}] : Fail to check content length", v.Name);
                                    v.RunResult = RunResult.Fail_CheckContentLength;
                                    return;
                                }
                            }
                            

                            Task<bool> dlTask = dl.DownloadCab(v);
                            await dlTask;

                            if (dlTask.Result)
                            {
                                v.LastDownload = DateTime.Now;
                            }
                            else
                            {
                                logger?.LogError("[{VendorName}] : Failure, download", v.Name);
                                v.RunResult = RunResult.Fail_Download;
                                return;
                            }


                            try
                            {
                                Task<bool> fcTask = fc.IsSame(v);
                                await fcTask;
                                if (fcTask.Result)
                                {
                                    logger?.LogInformation("[{VendorName}] : Cab files are the same, no action", v.Name);
                                    v.RunResult = RunResult.Success_NoChange;
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                logger?.LogError(ex, "[{vendorname}] : Fail to compare cab files", v.Name);
                                v.RunResult = RunResult.Fail_Compare;
                                return;
                            }
                            v.HasChange = true;
                            
                            
                            Task<bool> ceTask = ce.ExtractXml(v);
                            await ceTask;
                            if (!ceTask.Result)
                            {
                                logger?.LogError("[{VendorName}] : Failure, extract cab file", v.Name);
                                v.RunResult = RunResult.Fail_ExtractXml;
                                return;
                            }


                            var impTask = imp.ImportFromXml(v);
                            if (impTask.Result)
                            {
                                logger?.LogInformation("[{VendorName}] : Success, import XML file", v.Name);
                                v.RunResult = RunResult.Success_Imported;
                            }
                            else
                            {
                                logger?.LogError("[{VendorName}] : Failure, import XML file", v.Name);
                                v.RunResult = RunResult.Fail_Import;
                            }


                            bool r = ce.DeleteXml(v);
                            if (r)
                            {
                                logger?.LogInformation("[{VendorName}] : Deleted temporary XML file", v.Name);
                            }
                            else
                            {
                                logger.LogError("[{vn}] : Fail to delete temporary XML file", v.Name);
                            }
                        })
                    );
                }
                else
                {
                    logger?.LogInformation("[{VendorName}] : Not eligible, skip", v.Name);
                }
            }
            try
            {
                bool result = Task.WaitAll(tasks.ToArray(), runTimeoutInSeconds * 1000);
                if (result)
                {
                    logger?.LogInformation("Run finish in time");
                }
                else
                {
                    logger?.LogError("Run Error");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Run Error");
            }
            WriteFlagFile();
            CleanupAndRenameDir();
            
            if (dumpVendorProfileAfterRun)
            {
                DumpVendorProfile();
            }

            if (aTimer.Interval != runIntervalInSeconds*1000)
            {
                aTimer.Interval = runIntervalInSeconds*1000;
                logger?.LogInformation("Change timer wakeup interval to {interval} seconds", runIntervalInSeconds);
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
                {
                    if (immediateRunAfterStart)
                    {
                        RunOnce();
                    }
                    aTimer.Start();
                }
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
