using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    public class AImporter : IDisposable
    {
        public enum RunResult
        {
            Success_NoChange = 0,
            Success_Imported,
            Partial_Success_Imported,
            Fail_CheckContentLength,
            Fail_Download,
            Fail_ExtractXml,
            Fail_ExtractSdp,
            Fail_Compare,
            All_Fail_Import,
            Fail_Parsing_XML
        }

        private enum LogProvider
        {
            Console = 0,
            File
        }

        private static VendorProfile[] InitialProfiles = new VendorProfile[] {
            new VendorProfile("Dell", @"https://downloads.dell.com/Catalog/DellSDPCatalogPC.cab", true),
            new VendorProfile("Fujitsu", @"https://support.ts.fujitsu.com/GFSMS/globalflash/FJSVUMCatalogForSCCM.cab", true),
            new VendorProfile("HP", @"https://hpia.hpcloud.hp.com/downloads/sccmcatalog/HpCatalogForSms.latest.cab", true),
            new VendorProfile("Lenovo", @"https://download.lenovo.com/luc/v2/LenovoUpdatesCatalog2v2.cab", true),
            new VendorProfile("DellServer", @"https://downloads.dell.com/Catalog/DellSDPCatalog.cab", true),
            new VendorProfile("HPEnterprise", @"https://downloads.hpe.com/pub/softlib/puc/hppuc.cab", true)
        };

        //private readonly System.Timers.Timer aTimer;

        private readonly Downloader dl;

        private readonly CabExtractor ce;

        private readonly FileComparer fc;

        private readonly IImporter imp;

        private ILoggerFactory loggerFactory;

        private ILogger logger;

        private IDirFinder dirFinder;

        private List<VendorProfile> vendors = new List<VendorProfile>();

        private readonly bool isLaunchBackgroundService;



        // configuration options:

        // options about config file:
        private bool parseConfigFileOnEveryRun = true;

        // options about vendor profile override
        private string vendorProfileOverrideFilePath = null;
        private bool parseVendorProfileOverrideOnEveryRun = true;
        private bool vendorProfileOverrideAdditiveOnly = true;

        // options about verboseness
        private bool printVendorProfileInLogOnEveryRun = false;
        private bool dumpVendorProfileAfterRun = true;

        // options about scheduling:
        private bool immediateRunAfterStart = true;
        private int runIntervalInSeconds = 3 * 60;
        private int runTimeoutInSeconds = 300 * 60;

        // options about logging:
        private LogLevel minLogLevel = LogLevel.Debug;

        // options about WSUS
        private IPEndPoint wsusEndPoint = null;
        private bool useWsusImport = true;
        private bool asyncProcessEachPackage = true;
        private bool updateSusdbForVisibilityInConsole = false;
        private bool asyncUpdateSusdb = false;
        private bool publishOrDelete = true;
        private bool artificiallyMarshalDetectoid = true;
        private bool onlyOneDetectoidInLenovo = true;

        // options about Proxy
        private Uri proxy = null;


        // ------  End of Configuration Options ------


        public AImporter(bool isBackgroundSerive)
        {
            isLaunchBackgroundService = isBackgroundSerive;
            
            if (isLaunchBackgroundService)
                dirFinder = new BackgroundServiceDirFinder(AppContext.BaseDirectory);
            else
                //dirFinder = new SimpleDirFinder(@"C:\Temp\");
                dirFinder = new ServerDirFinder();

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

            logger?.LogInformation("===========================================");
            logger?.LogDebug("Initialize Driver Catalog Importer, run as {mode}", isLaunchBackgroundService? "background Windows Service" : "console application");

            try
            {
                dl = new Downloader(loggerFactory.CreateLogger<Downloader>(), dirFinder, proxy);
            }
            catch (Exception e)
            {
                logger?.LogError(e, "Fail to create downloader\n");
                throw new Exception("Fail to create downloader");
            }

            try
            {
                ce = new CabExtractor(loggerFactory.CreateLogger<CabExtractor>(), dirFinder);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Fail to create cab extractor\n");
                throw new Exception("Fail to create cab extractor");
            }

            try
            {
                fc = new FileComparer(loggerFactory.CreateLogger<FileComparer>(), dirFinder);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Fail to create file comparer\n");
                throw new Exception("Fail to file comparer");
            }

            try
            {
                if (useWsusImport)
                    imp = new WsusImporter(loggerFactory.CreateLogger<WsusImporter>(), dirFinder, wsusEndPoint);
                else
                    imp = new DefaultImporter(loggerFactory.CreateLogger<DefaultImporter>(), dirFinder);
            }
            catch (Exception e)
            {
                logger?.LogError(e, "Fail to create importer\n");
                throw new Exception("Fail to create importer");
            }
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
            string formatS = "{0,-20}{1,-" + fieldWidth + "}{2,-15}{3,-12}{4,-30}";
            return formatS;
        }

        private void PrintVendorProfile()
        {
            string formatS = GetVendorProfileFormatString();
            logger?.LogInformation("Current Vendor Profile:");
            logger?.LogInformation(formatS, "Name", "URL", "Eligible", "FileSize", "LastDownload");
            foreach (VendorProfile v in vendors)
            {
                logger?.LogInformation(formatS, v.Name, v.DownloadUri.AbsoluteUri, v.Eligible, v.NewContentLength.ToString(), v.LastDownload.ToString());
            }
        }

        private void DumpVendorProfile()
        {
            string filePath = Path.Combine(dirFinder.GetVendorProfileOverrideFileDir(), "EffectiveVendorProfile.txt");
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
                        sw.WriteLine(formatS, v.Name, v.DownloadUri.AbsoluteUri, v.Eligible, v.NewContentLength.ToString(), v.LastDownload.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogCritical(ex, "Fail to write flag file\n");
            }
        }

        private void ParseConfigOptions(string k, string v)
        {
            if (k.Equals("ParseConfigFileOnEveryRun", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    parseConfigFileOnEveryRun = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"ParseConfigFileOnEveryRun\" option, it has to be True or False\n");
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
            else if (k.Equals("ParseVendorProfileOverrideOnEveryRun", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    parseVendorProfileOverrideOnEveryRun = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"ParseVendorProfileOverrideOnEveryRun\" option, it has to be True or False\n");
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
                    logger?.LogError(e, "Invalid value for \"VendorProfileOverrideAdditiveOnly\" option, it has to be True or False\n");
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
                    logger?.LogError(e, "Invalid value for \"PrintVendorProfileInLogOnEveryRun\" option, it has to be True or False\n");
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
                    logger?.LogError(e, "Invalid value for \"DumpVendorProfileAfterRun\" option, it has to be True or False\n");
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
                    logger?.LogError(e, "Invalid value for \"ImmediateRunAfterStart\" option, it has to be True or False\n");
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
                    logger?.LogError(e, "Invalid value for \"RunIntervalInSeconds\" option, it has to be an integer\n");
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
                    logger?.LogError(e, "Invalid value for \"RunTimeoutInSeconds\" option, it has to be an integer\n");
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
            else if (k.Equals("UseWsusImport", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    useWsusImport = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"UseWsusImport\" option, it has to be True or False\n");
                }
            }
            else if (k.Equals("WsusEndPoint", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    if (v.Contains(":"))
                    {
                        var parts = v.Split(':');
                        wsusEndPoint = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
                    }
                    else
                    {
                        wsusEndPoint = new IPEndPoint(IPAddress.Parse(v), 0);
                    }
                }
                catch (Exception e)
                {
                    logger?.LogError(e, "Invalid value for \"WsusEndPoint\" option, it has to be in the 1.1.1.1 or 1.1.1.1:80 format\n");
                }
            }
            else if (k.Equals("AsyncProcessEachPackage", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    asyncProcessEachPackage = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"AsyncProcessEachPackage\" option, it has to be True or False\n");
                }
            }
            else if (k.Equals("UpdateSusdbForVisibilityInConsole", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    updateSusdbForVisibilityInConsole = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"UpdateSusdbForVisibilityInConsole\" option, it has to be True or False\n");
                }
            }
            else if (k.Equals("AsyncUpdateSusdb", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    asyncUpdateSusdb = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"AsyncUpdateSusdb\" option, it has to be True or False\n");
                }
            }
            else if (k.Equals("PublishOrDelete", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    publishOrDelete = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"PublishOrDelete\" option, it has to be True or False\n");
                }
            }
            else if (k.Equals("ArtificiallyMarshalDetectoid", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    artificiallyMarshalDetectoid = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"ArtificiallyMarshalDetectoid\" option, it has to be True or False\n");
                }
            }
            else if (k.Equals("OnlyOneDetectoidInLenovo", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    onlyOneDetectoidInLenovo = bool.Parse(v);
                }
                catch (FormatException e)
                {
                    logger?.LogError(e, "Invalid value for \"OnlyOneDetectoidInLenovo\" option, it has to be True or False\n");
                }
            }
            else if (k.Equals("Proxy", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    proxy = new Uri(v);
                }
                catch (Exception e)
                {
                    logger?.LogError(e, "Invalid value for \"Proxy\" option, it has to be in the http://1.1.1.1:80 or https://1.1.1.1:80 format\n");
                }
            }
            else
            {
                logger?.LogDebug("Unknown option: {opt}", k);
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
                foreach (var e in entries)
                {
                    var entry = e.Trim();
                    if (String.IsNullOrWhiteSpace(entry))
                        continue;
                    if (entry.StartsWith("//"))
                        continue;
                    
                    var parts = entry.Split('\t');
                    if (parts.Length == 2)
                    {
                        string k = parts[0];
                        string v = parts[1];
                        ParseConfigOptions(k, v);
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
                filePath = Path.Combine(dirFinder.GetVendorProfileOverrideFileDir(), "VendorProfileOverride.txt");
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
                        logger?.LogError(ex, "Wrong syntax in the eligible field for vendor profile at line {line}\n", lineNumber);
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

        private void ParseDumpedVendorProfile()
        {
            string filePath = Path.Combine(dirFinder.GetVendorProfileOverrideFileDir(), "EffectiveVendorProfile.txt");
            if (!File.Exists(filePath))
            {
                logger?.LogWarning("Previous run did not dump vendor profiles, cannot get last download time, will download cab files");
                return;
            }
            var entries = File.ReadAllLines(filePath);
            StringBuilder sb = new StringBuilder();
            foreach (var entry in entries)
            {
                int counter = 0;
                sb.Clear();
                var parts = entry.Split();
                string name = "";
                int filesize = 0;
                foreach(var part in parts)
                {
                    if (string.IsNullOrEmpty(part)) 
                        continue;
                    
                    counter++;
                    if (counter == 1)
                        name = part;
                    
                    if (counter == 4)
                        filesize = int.Parse(part, NumberStyles.Integer);
                    
                    if (counter >= 5)
                        sb.Append(part).Append(' ');
                }
                foreach(var v in vendors)
                {
                    if (v.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        v.LastDownload = DateTime.Parse(sb.ToString());
                        v.OldContentLength = filesize;
                        break;
                    }
                }
            }
        }

        private void CreateLogger(LogProvider p)
        {
            if (p == LogProvider.Console)
            {
                loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                logger = loggerFactory.CreateLogger<AImporter>();
            }
            else if (p == LogProvider.File)
            {
                string logFilePath = Path.Combine(dirFinder.GetLogFileDir(), "DriverCatalogImport.log");
                loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new FileLoggerProvider(logFilePath, new FileLoggerOptions()
                {
                    FileSizeLimitBytes = 5 * 1024 * 1024,
                    MaxRollingFiles = 5,
                    Append = true,
                    MinLevel = minLogLevel,
                    UseUtcTimestamp = true
                }));
                logger = loggerFactory.CreateLogger<AImporter>();
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
            string flagFilePath = Path.Combine(dirFinder.GetFlagFileDir(), "DriverCatalogsUpdated.txt");
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
                        if (v.Eligible)
                        {
                            sw.WriteLine("{0}\t{1}", v.Name, v.RunResult.ToString());
                        }
                        else
                        {
                            sw.WriteLine("{0}\tSkipped", v.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogCritical(ex, "Fail to write flag file\n");
            }
        }

        public async Task RunOnce()
        {
            if (isLaunchBackgroundService)
            {
                logger?.LogInformation("===========================================");
            }

            if (parseConfigFileOnEveryRun)
            {
                ParseConfigFile();
            }
            if (parseVendorProfileOverrideOnEveryRun)
            {
                ParseVendorProfileOverrideFile();
            }
            if (printVendorProfileInLogOnEveryRun)
            {
                PrintVendorProfile();
            }

            ParseDumpedVendorProfile(); // get last download timestamp

            LinkedList<Task> tasks = new LinkedList<Task>();

            foreach (VendorProfile v in vendors)
            {
                if (v.Eligible)
                {
                    logger?.LogInformation("[{VendorName}] : Eligible, Start task", v.Name);
                    tasks.AddLast(
                        Task.Run(async () =>
                        {
                            TimeSpan? interval = DateTime.Now - v.LastDownload;
                            bool contentLengthDifferent = false;
                            if (interval?.TotalHours < 24)
                            {
                                Task<bool> headerTask = dl.CheckCabSize(v);
                                await headerTask;
                                if (headerTask.Result)
                                {
                                    if (v.OldContentLength == v.NewContentLength)
                                    {
                                        logger?.LogInformation("[{vn}] : Content-Length remains the same", v.Name);
                                        v.RunResult = RunResult.Success_NoChange;
                                        return;
                                    }
                                    else
                                    {
                                        logger?.LogInformation("[{vn}] : old content-length: {oldlen}, new content-length: {newlen}", v.Name, v.OldContentLength, v.NewContentLength);
                                        contentLengthDifferent = true;
                                        // proceed to download cab file
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

                            if (contentLengthDifferent)
                            {
                                v.HasChange = true;
                            }
                            else
                            {
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
                                    logger?.LogError(ex, "[{vendorname}] : Fail to compare cab files\n", v.Name);
                                    v.RunResult = RunResult.Fail_Compare;
                                    return;
                                }
                                v.HasChange = true;
                            }
                            
                            Task<bool> ceTask = ce.ExtractXml(v);
                            await ceTask;
                            if (!ceTask.Result)
                            {
                                logger?.LogError("[{VendorName}] : Failure, extract cab file", v.Name);
                                v.RunResult = RunResult.Fail_ExtractXml;
                                ce.DeleteXml(v);
                                return;
                            }



                        }) // Task.Run( async () => {
                    ); // tasks.Add
                }
                else
                {
                    logger?.LogInformation("[{VendorName}] : Not eligible, skip", v.Name);
                }
            } // foreach (VendorProfile v in vendors)
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error before invoking WSUS\n");
            }

            foreach(VendorProfile v in vendors)
            {
                if (v.Eligible)
                {
                    if (v.HasChange)
                    {
                        var impResult = imp.ImportFromXml(v, new ImportInstructions(asyncProcessEachPackage, updateSusdbForVisibilityInConsole, asyncUpdateSusdb, publishOrDelete, artificiallyMarshalDetectoid, onlyOneDetectoidInLenovo));
                        if (impResult.Total > 0)
                        {
                            if (impResult.Total == impResult.Success)
                            {
                                logger?.LogInformation("[{VendorName}] : Success, import from XML file", v.Name);
                                v.RunResult = RunResult.Success_Imported;
                            }
                            else if (impResult.Total > impResult.Success && impResult.Success > 0)
                            {
                                logger?.LogInformation("[{vn}] : Partial Success, import from XML file", v.Name);
                                v.RunResult = RunResult.Partial_Success_Imported;
                            }
                            else if (impResult.Success == 0)
                            {
                                logger?.LogCritical("[{vn}] : All Fail, import from XML file", v.Name);
                                v.RunResult = RunResult.All_Fail_Import;
                            }
                        }
                        else
                        {
                            logger?.LogError("[{VendorName}] : Failure, import XML file", v.Name);
                            v.RunResult = RunResult.Fail_Parsing_XML;
                        }
                        logger?.LogInformation("[{vn}] : Import result: Total: {t}, Success: {s}, Fail: {f}", v.Name, impResult.Total, impResult.Success, impResult.Failure);

                        bool r = ce.DeleteXml(v);
                        if (r)
                        {
                            logger?.LogInformation("[{VendorName}] : Deleted temporary XML file", v.Name);
                        }
                        else
                        {
                            logger?.LogError("[{vn}] : Fail to delete temporary XML file", v.Name);
                        }
                    }
                }
            }

            WriteFlagFile();
            CleanupAndRenameDir();
            
            if (dumpVendorProfileAfterRun)
            {
                DumpVendorProfile();
            }
        }

        public void Dispose()
        {
            loggerFactory.Dispose();
            //aTimer.Dispose();
        }
    }
}
