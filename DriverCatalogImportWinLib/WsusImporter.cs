using Microsoft.Extensions.Logging;
using Microsoft.UpdateServices.Administration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace DriverCatalogImporter
{
    internal class SinglePackagePublishInstruction
    {
        public VendorProfile Vp { get; set; }

        public bool CreateTempSdpFile { get; set; }
        public bool DeleteTempSdpFile { get; set; }
        public string TempSdpFilePath { get; set; }

        public bool ArtificiallyMarshalDetectoid { get; set; }
        public bool UpdateSusdbForVisibility { get; set; }
        public bool AsyncUpdateSusdb {  get; set; }
        public SqlConnection SqlCon { get; set; }

        public SinglePackagePublishInstruction(VendorProfile vp, bool createTempSdpFile, bool deleteTempSdpFile, string tempSdpFilePath, bool artificiallyMarshalDetectoid, bool updateSusdbForVisibility, bool asyncUpdateSusdb, SqlConnection sqlCon)
        {
            CreateTempSdpFile = createTempSdpFile;
            DeleteTempSdpFile = deleteTempSdpFile;
            UpdateSusdbForVisibility = updateSusdbForVisibility;
            AsyncUpdateSusdb = asyncUpdateSusdb;
            SqlCon = sqlCon;
            TempSdpFilePath = tempSdpFilePath;
            Vp = vp;
            ArtificiallyMarshalDetectoid = artificiallyMarshalDetectoid;
        }
    }

    internal class WsusImporter : IImporter
    {
        private readonly IUpdateServer wsus;
        private readonly ILogger logger;
        private readonly IDirFinder dirFinder;
        private readonly string ServerName = "\\\\.\\pipe\\Microsoft##WID\\tsql\\query";
        private readonly string DataBaseName = "SUSDB";
        private readonly string ConnectionStr;
        private readonly int batchSize = 500;
        
        public WsusImporter(ILogger _logger, IDirFinder _dirFinder) : this (_logger, _dirFinder, null) { }
        public WsusImporter(ILogger _logger, IDirFinder _dirFinder, IPEndPoint _wsusEndPoint)
        {
            if (_wsusEndPoint != null)
            {
                if (_wsusEndPoint.Port != 0)
                {
                    wsus = AdminProxy.GetUpdateServer(_wsusEndPoint.Address.ToString(), false, _wsusEndPoint.Port); // AdminProxy thread safty info, see Microsoft doc
                }
                else
                {
                    wsus = AdminProxy.GetUpdateServer(_wsusEndPoint.Address.ToString(), false);
                }
            }
            else
            {
                wsus = AdminProxy.GetUpdateServer();
            }
            
            ConnectionStr = string.Format("Server={0};Database={1};Integrated Security=sspi;Pooling=true", ServerName, DataBaseName);
            logger = _logger;
            dirFinder = _dirFinder;
        }

        private void MarshalDetectoid(SoftwareDistributionPackage sdp, VendorProfile vp)
        {
            InstallableItem item = new InstallableItem
            {
                Id = Guid.NewGuid(),
                IsInstallableApplicabilityRule = @"<lar:False />",
                InstallBehavior = new InstallBehavior
                {
                    CanRequestUserInput = false,
                    RequiresNetworkConnectivity = false,
                    Impact = InstallationImpact.Normal,
                    RebootBehavior = RebootBehavior.NeverReboots
                },
                OriginalSourceFile = new FileForInstallableItem
                {
                    Digest = "2YJspaMCUFCwXw7vwswjTo9RusE=",
                    FileName = "dummy.exe",
                    OriginUri = new Uri(@"https://download.dummy.com/dummy"),
                    Size = 123456,
                    Modified = new DateTime(2020, 1, 1, 1, 1, 1)
                }
            };
            item.Languages.Add("en");
            sdp.InstallableItems.Add(item);
            sdp.PackageUpdateType = PackageUpdateType.Software;
            sdp.Title = "[DETECTOID] " + sdp.Title;
            logger.LogWarning("[{vn}] : Detectoid is marshaled, {pkgid}", vp.Name, sdp.PackageId.ToString());
        }

        /**
         * Calling GetUpdate to check the existence before calling DeleteUdpate doesn't really gain much.
         * Directly calling DeleteUpdate gets the job done, and if the input update doesn't exist,
         * DeleteUpdate throws WsusObjectNotFoundException, which is the same effect as calling GetUpdate
         */
        private bool DeleteOnePackage(VendorProfile vp, SoftwareDistributionPackage sdp)
        {
            bool success;
            try
            {
                wsus.DeleteUpdate(sdp.PackageId);
                logger.LogInformation("[{vn}] : Success, delete package {pkgid}", vp.Name, sdp.PackageId.ToString());
                success = true;
            }
            catch (WsusObjectNotFoundException)
            {
                logger.LogInformation("[{vn}] : Success, Delete, No action, package not exist {pkgid}", vp.Name, sdp.PackageId.ToString());
                success = true;
            }
            catch (Exception e1)
            {
                logger.LogError(e1, "[{vn}] : Fail, delete package {pkgid}\n", vp.Name, sdp.PackageId.ToString());
                success = false;
            }
            return success;
        }

        private async Task<bool> PublishOnePackage(SoftwareDistributionPackage sdp, SinglePackagePublishInstruction instruct)
        {
            bool success = false;
            bool pkgExist;

            string tmpSdpFilePath;
            if (instruct.CreateTempSdpFile)
            {
                tmpSdpFilePath = Path.Combine(dirFinder.GetTmpSdpFileDir(), sdp.PackageId.ToString());
                tmpSdpFilePath = Path.ChangeExtension(tmpSdpFilePath, ".sdp");
            }
            else
            {
                tmpSdpFilePath = instruct.TempSdpFilePath;
            }

            if (sdp.PackageUpdateType == PackageUpdateType.Detectoid)
            {
                pkgExist = wsus.IsPrerequisitePresent(sdp.PackageId); // if it's detectoid, this is the way to check its own existence
            }
            else
            {
                try
                {
                    wsus.GetUpdate(new UpdateRevisionId(sdp.PackageId, 0));  // IUpdateServer thread safty info not provided in Microsoft doc
                    logger.LogTrace("[{vn}] : udpate exist {pkgid}", instruct.Vp.Name, sdp.PackageId.ToString());
                    pkgExist = true;
                }
                catch
                {
                    logger.LogTrace("[{vn}] : udpate not exist {pkgid}", instruct.Vp.Name, sdp.PackageId.ToString());
                    pkgExist = false;
                }
            }

            if (pkgExist)
            {
                try
                {
                    if (sdp.PackageUpdateType == PackageUpdateType.Detectoid && instruct.ArtificiallyMarshalDetectoid)
                    {
                        MarshalDetectoid(sdp, instruct.Vp);
                    }
                    if (instruct.CreateTempSdpFile)
                    {
                        sdp.Save(tmpSdpFilePath);
                        logger.LogTrace("[{vn}] : saved temporary sdp file {pkgid}", instruct.Vp.Name, sdp.PackageId.ToString());
                    }
                    IPublisher publisher = wsus.GetPublisher(tmpSdpFilePath);
                    if (sdp.PackageUpdateType == PackageUpdateType.Detectoid || sdp.ProductNames.Contains("Bundles"))
                    {
                        publisher.MetadataOnly = false;
                    }
                    else
                    {
                        publisher.MetadataOnly = true;
                    }
                    publisher.RevisePackage();
                    logger.LogInformation("[{vn}] : Success, revise package {pkgid}", instruct.Vp.Name, sdp.PackageId.ToString());
                    success = true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[{vn}] : Fail, revise package {pkgid}\n", instruct.Vp.Name, sdp.PackageId.ToString());
                }
            }
            else
            {
                bool isPreReqDefined, isPreReqOnWsus = true;
                var prereq = sdp.Prerequisites;
                if (prereq != null && prereq.Count() > 0)
                {
                    isPreReqDefined = true;
                    try
                    {
                        IList<PrerequisiteGroup> prereqGroup = sdp.Prerequisites;
                        foreach(PrerequisiteGroup group in prereqGroup)
                        {
                            IList<Guid> ids = group.Ids;
                            foreach(Guid id in ids)
                            {
                                if (!wsus.IsPrerequisitePresent(id))
                                {
                                    isPreReqOnWsus = false;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "[{vn}] : Fail, inquire prerequisite, {pkgid}", instruct.Vp.Name, sdp.PackageId.ToString());
                        return false;
                    }
                }
                else
                {
                    isPreReqDefined = false;
                    logger.LogTrace("[{vn}] : package has no prerequisite {pkgid}", instruct.Vp.Name, sdp.PackageId.ToString());
                }

                try
                {
                    if ((!isPreReqDefined) || (isPreReqDefined && isPreReqOnWsus))
                    {
                        if (sdp.PackageUpdateType == PackageUpdateType.Detectoid && instruct.ArtificiallyMarshalDetectoid)
                        {
                            MarshalDetectoid(sdp, instruct.Vp);
                        }
                        if (instruct.CreateTempSdpFile)
                        {
                            sdp.Save(tmpSdpFilePath);

                            logger.LogTrace("[{vn}] : saved temporary sdp file {pkgid}", instruct.Vp.Name, sdp.PackageId.ToString());
                        }
                        IPublisher publisher = wsus.GetPublisher(tmpSdpFilePath);

                        if (sdp.PackageUpdateType == PackageUpdateType.Detectoid || sdp.ProductNames.Contains("Bundles"))
                        {
                            publisher.MetadataOnly = false;
                        }
                        else
                        {
                            publisher.MetadataOnly = true;
                        }
                        
                        publisher.PublishPackage(null, null);
                        logger.LogInformation("[{vn}] : Success, publish package {pkgid}", instruct.Vp.Name, sdp.PackageId.ToString());
                        success = true;
                    }
                    else
                    {
                        logger.LogError("[{vn}] : Fail, publish package {pkgid}, prerequisite missing", instruct.Vp.Name, sdp.PackageId.ToString());
                        return false;
                    }
                }
                catch (Exception e1)
                {
                    logger.LogError(e1, "[{vn}] : Fail, publish package {pkgid}\n", instruct.Vp.Name, sdp.PackageId.ToString());
                }
            }
            if (instruct.DeleteTempSdpFile)
            {
                File.Delete(tmpSdpFilePath);
                logger.LogTrace("[{vn}] : Deleted temporary sdp file", instruct.Vp.Name);
            }
            if (instruct.UpdateSusdbForVisibility && (!pkgExist) && success)
            {
                if (instruct.AsyncUpdateSusdb)
                {
                    var t = UpdateDatabaseAsync(sdp.PackageId.ToString(), instruct.SqlCon);
                    await t;
                    if (!t.Result)
                    {
                        logger.LogError("[{vn}] : Fail, update database", instruct.Vp.Name);
                    }
                }
                else
                {
                    bool r = UpdateDatabase(sdp.PackageId.ToString(), instruct.SqlCon);
                    if (!r)
                    {
                        logger.LogError("[{vn}] : Fail, update database", instruct.Vp.Name);
                    }
                }
            }
            return success;
        }

        public ImportStats ImportFromXml(VendorProfile vp, ImportInstructions instruct)
        {
            string xmlFileName = Path.ChangeExtension(vp.CabFileName, ".xml");
            
            string xmlFilePath = Path.Combine(dirFinder.GetCabExtractOutputDir(), xmlFileName);

            if (File.Exists(xmlFilePath))
            {
                logger.LogDebug("[{vn}] : Start parsing and importing XML file", vp.Name);
            }
            else
            {
                logger.LogError("[{vn}] : XML file does not exist", vp.Name);
                return new ImportStats(); //Task.FromResult(new ImportStats());
            }

            ConcurrentQueue<SoftwareDistributionPackage> extractedSdps = new ConcurrentQueue<SoftwareDistributionPackage>();
            
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(new NameTable());
            try
            {
                nsmgr.AddNamespace("", "http://www.w3.org/2001/XMLSchema");
                nsmgr.AddNamespace("smc", "http://schemas.microsoft.com/sms/2005/04/CorporatePublishing/SystemsManagementCatalog.xsd");
            }
            catch (Exception e) 
            {
                logger.LogError(e, "[{vn}] : Fail to load XML namespace\n", vp.Name);
                return  new ImportStats();  //Task.FromResult(new ImportStats());
            }

            XmlDocument xmlDoc = new XmlDocument();
                
            try
            {
                xmlDoc.Load(xmlFilePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{vn}] : Fail to load XML file\n", vp.Name);
                return new ImportStats(); //Task.FromResult(new ImportStats());
            }

            try
            {
                var xmlNodeList = xmlDoc.SelectNodes("smc:SystemsManagementCatalog/smc:SoftwareDistributionPackage", nsmgr);
                    
                if (xmlNodeList != null)
                {
                    Parallel.ForEach(xmlNodeList.Cast<XmlNode>(), node =>
                    {
                        XPathNavigator navigator = node.CreateNavigator();
                        extractedSdps.Enqueue(new SoftwareDistributionPackage(navigator)); 
                        // this constructor is obsolete, but the constructor that is not obsolete does not work!
                        
                    });
                }
                else
                {
                    logger.LogWarning("[{vn}] : no software distribution package in XML file, please check the XML file", vp.Name);
                    return new ImportStats();
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "[{vn}] : Fail to find relevant nodes in XML file\n", vp.Name);
                return new ImportStats(); //Task.FromResult(new ImportStats());
            }

            ImportStats stats = new ImportStats
            {
                Total = extractedSdps.Count()
            };
            
            logger.LogInformation("[{vn}] : Total software distribution packages: {t}", vp.Name, extractedSdps.Count());

            // a special processing for Dell Server packages, change their VendorName field so that they are distinguishable from Dell PC packages in WSUS
            if (vp.Name.Equals("DellServer", StringComparison.CurrentCultureIgnoreCase))
            {
                foreach(SoftwareDistributionPackage sdp in extractedSdps)
                {
                    sdp.VendorName = vp.Name;
                }
            }

            SqlConnection sqlCon = null;
            if (instruct.UpdateSusdbForVisibilityInConsole)
            {
                sqlCon = new SqlConnection(ConnectionStr);
                sqlCon.Open();
                logger.LogInformation("[{vn}] : SQL connection is opened", vp.Name);
            }

            
            if (vp.Name.Equals("Lenovo", StringComparison.CurrentCultureIgnoreCase) && instruct.AsyncProcessEachPackage && instruct.OnlyOneDetectoidInLenovo)
            {
                logger.LogCritical("[{vn}] : Async process packages is selected, catalog may have prerequisites implications, program is instructed that there is only 1 detectoid in catalog, need to process the detectoid first then async process other packages", vp.Name);
                if (instruct.PublishOrDelete)
                {
                    foreach (SoftwareDistributionPackage sdp in extractedSdps)
                    {
                        if (sdp.PackageUpdateType == PackageUpdateType.Detectoid)
                        {
                            SinglePackagePublishInstruction singlePkgInstruct = new SinglePackagePublishInstruction(vp, true, true, null, instruct.ArtificiallyMarshalDetectoid, instruct.UpdateSusdbForVisibilityInConsole, instruct.AsyncUpdateSusdb, sqlCon);

                            Task<bool> t = PublishOnePackage(sdp, singlePkgInstruct);
                            t.Wait();
                            if (t.Result)
                                stats.Success++;
                            else
                                stats.Failure++;
                            break;
                        }
                    }

                    var sdpArray = extractedSdps.ToArray();
                    int i = 0, batch = 1;
                    while (i < sdpArray.Length)
                    {
                        LinkedList<Task<bool>> tasks = new LinkedList<Task<bool>>();

                        for (; i < batch * batchSize && i < sdpArray.Length; i++)
                        {
                            var sdp = sdpArray[i];
                            if (sdp.PackageUpdateType != PackageUpdateType.Detectoid)
                            {
                                SinglePackagePublishInstruction singlePkgInstruct = new SinglePackagePublishInstruction(vp, true, true, null, instruct.ArtificiallyMarshalDetectoid, instruct.UpdateSusdbForVisibilityInConsole, instruct.AsyncUpdateSusdb, sqlCon);
                                tasks.AddLast(PublishOnePackage(sdp, singlePkgInstruct));
                            }
                        }
                        Task.WaitAll(tasks.ToArray());
                        foreach (Task<bool> t in tasks)
                        {
                            if (t.Result)
                            {
                                stats.Success++;
                            }
                            else
                                stats.Failure++;
                        }
                        batch++;
                    }
                }
                else // delete
                {
                    var sdpArray = extractedSdps.ToArray();
                    int i = 0, batch = 1;
                    int indexOfDetectoid = 0;
                    while (i < sdpArray.Length)
                    {
                        LinkedList<Task<bool>> tasks = new LinkedList<Task<bool>>();

                        for (; i < batch * batchSize && i < sdpArray.Length; i++)
                        {
                            var sdp = sdpArray[i];
                            if (sdp.PackageUpdateType != PackageUpdateType.Detectoid)
                            {
                                SinglePackagePublishInstruction singlePkgInstruct = new SinglePackagePublishInstruction(vp, true, true, null, instruct.ArtificiallyMarshalDetectoid, instruct.UpdateSusdbForVisibilityInConsole, instruct.AsyncUpdateSusdb, sqlCon);
                                tasks.AddLast(Task.Run(() => DeleteOnePackage(vp, sdp)));
                            }
                            else
                            { indexOfDetectoid = i; }
                        }
                        Task.WaitAll(tasks.ToArray());
                        foreach (Task<bool> t in tasks)
                        {
                            if (t.Result)
                            {
                                stats.Success++;
                            }
                            else
                                stats.Failure++;
                        }
                        batch++;
                    }

                    bool r = DeleteOnePackage(vp, sdpArray[indexOfDetectoid]);
                    if (r)
                        stats.Success++;
                    else
                        stats.Failure++;
                }
                return stats;
            }
            
            if (vp.Name.Equals("Lenovo", StringComparison.CurrentCultureIgnoreCase) || vp.Name.Equals("DellServer", StringComparison.CurrentCultureIgnoreCase))
            {
                if (instruct.AsyncProcessEachPackage)
                    logger.LogCritical("[{vn}] : Async processing packages is selected, but catalog has prerequisites implications, override to sync process", vp.Name);

                instruct.AsyncProcessEachPackage = false;

                SinglePackagePublishInstruction singlePkgInstruct = new SinglePackagePublishInstruction(vp, true, true, null, instruct.ArtificiallyMarshalDetectoid, instruct.UpdateSusdbForVisibilityInConsole, instruct.AsyncUpdateSusdb, sqlCon);

                try
                {
                    if (instruct.PublishOrDelete)
                    {
                        DfsPublish(extractedSdps, singlePkgInstruct, stats);
                    }
                    else
                    {
                        DfsDelete(extractedSdps, singlePkgInstruct, stats);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "[{vn}] : Fail, publish/delete with ordering", vp.Name);
                }
                return stats;
            }
            

            if (instruct.AsyncProcessEachPackage)
            {
                var sdpArray = extractedSdps.ToArray();
                int i = 0, batch = 1;
                while (i < sdpArray.Length)
                {
                    LinkedList<Task<bool>> tasks = new LinkedList<Task<bool>>();

                    for (; i < batch*batchSize && i < sdpArray.Length ; i++)
                    {
                        if (instruct.PublishOrDelete)
                        {
                            // must not put 'i' inside the lambda, because lambda is not synchronously invoked,
                            // many iterations could have passed by when lambda is invoked, and when it's invoked, i's value is undetermined
                            var sdp = sdpArray[i];
                            SinglePackagePublishInstruction singlePkgInstruct = new SinglePackagePublishInstruction(vp, true, true, null,instruct.ArtificiallyMarshalDetectoid, instruct.UpdateSusdbForVisibilityInConsole, instruct.AsyncUpdateSusdb, sqlCon);
                            tasks.AddLast(PublishOnePackage(sdp, singlePkgInstruct));
                        }
                        else
                        {
                            var sdp = sdpArray[i];
                            tasks.AddLast(Task.Run(() => DeleteOnePackage(vp, sdp)));
                        }
                    }
                    Task.WaitAll(tasks.ToArray());
                    foreach (Task<bool> t in tasks)
                    {
                        if (t.Result)
                        {
                            stats.Success++;
                        }
                        else
                            stats.Failure++;
                    }
                    batch++;
                }
            }
            else
            {
                SinglePackagePublishInstruction singlePkgInstruct = new SinglePackagePublishInstruction(vp, true, true, null, instruct.ArtificiallyMarshalDetectoid, instruct.UpdateSusdbForVisibilityInConsole, instruct.AsyncUpdateSusdb, sqlCon);
                foreach (SoftwareDistributionPackage sdp in extractedSdps)
                {
                    if (instruct.PublishOrDelete)
                    {                        
                        Task<bool> t = PublishOnePackage(sdp, singlePkgInstruct);
                        t.Wait();
                        if (t.Result)
                        {
                            stats.Success++;
                        }
                        else
                            stats.Failure++;
                    }
                    else
                    {
                        bool r = DeleteOnePackage(vp, sdp);
                        if (r)
                            stats.Success++;
                        else
                            stats.Failure++;
                    }
                }
            }
            sqlCon?.Close();
            return stats;  //Task.FromResult(stats);
        }


        class SoftwareDistributionPackageAugment
        {
            public SoftwareDistributionPackage Sdp { get; set; }
            public bool isProcessed { get; set; }
        }

        private void DfsPublishRecur(Dictionary<Guid, SoftwareDistributionPackageAugment> d, SoftwareDistributionPackage sdp, SinglePackagePublishInstruction instruct, ImportStats stats)
        {
            IList<PrerequisiteGroup> groups = sdp.Prerequisites;
            foreach (PrerequisiteGroup g in groups)
            {
                IList<Guid> ids = g.Ids;
                foreach (Guid id in ids)
                {
                    SoftwareDistributionPackageAugment sdpA = d[id];
                    if (!sdpA.isProcessed)
                    {
                        DfsPublishRecur(d, sdpA.Sdp, instruct, stats);
                    }
                }
            }
            IList<Guid> bundledPackages = sdp.BundledPackages;
            foreach(Guid guid in bundledPackages)
            {
                SoftwareDistributionPackageAugment sdpA = d[guid];
                if (!sdpA.isProcessed)
                {
                    DfsPublishRecur(d, sdpA.Sdp, instruct, stats);
                }
            }
 
            Task<bool> t = PublishOnePackage(sdp, instruct);
            t.Wait();
            if (t.Result)
            {
                stats.Success++;
            }
            else
                stats.Failure++;
            d[sdp.PackageId].isProcessed = true;
        }

        private void DfsPublish(IEnumerable<SoftwareDistributionPackage> extractedSdps, SinglePackagePublishInstruction instruct, ImportStats stats)
        {
            Dictionary<Guid, SoftwareDistributionPackageAugment> d = new Dictionary<Guid, SoftwareDistributionPackageAugment>();
            foreach (SoftwareDistributionPackage sdp in extractedSdps)
            {
                d.Add(sdp.PackageId, new SoftwareDistributionPackageAugment { Sdp = sdp, isProcessed = false });
            }

            foreach (SoftwareDistributionPackage sdp in extractedSdps)
            {
                SoftwareDistributionPackageAugment sdpAug = d[sdp.PackageId];
                if (!sdpAug.isProcessed)
                {
                    DfsPublishRecur(d, sdp, instruct, stats);
                }
            }
        }


        class SoftwareDistributionPackageAugment2
        {
            public SoftwareDistributionPackage Sdp { get; set; }
            public readonly LinkedList<Guid> depend = new LinkedList<Guid>();
            public bool isProcessed { get; set; }
        }

        private void DfsDeleteRecur(Dictionary<Guid, SoftwareDistributionPackageAugment2> d, SoftwareDistributionPackage sdp, SinglePackagePublishInstruction instruct, ImportStats stats)
        {
            SoftwareDistributionPackageAugment2 sdpAug2 = d[sdp.PackageId];
            LinkedList<Guid> depend = sdpAug2.depend;
            foreach(Guid guid in depend)
            {
                if (!d[guid].isProcessed)
                {
                    DfsDeleteRecur(d, d[guid].Sdp, instruct, stats);
                }
            }
            bool r = DeleteOnePackage(instruct.Vp, sdp);
            if (r)
                stats.Success++;
            else
                stats.Failure++;
            sdpAug2.isProcessed = true;
        }

        private void DfsDelete(IEnumerable<SoftwareDistributionPackage> sdps, SinglePackagePublishInstruction instruct, ImportStats stats)
        {
            Dictionary<Guid, SoftwareDistributionPackageAugment2> d = new Dictionary<Guid, SoftwareDistributionPackageAugment2>();
            foreach(SoftwareDistributionPackage sdp in sdps)
            {
                d.Add(sdp.PackageId, new SoftwareDistributionPackageAugment2 { Sdp = sdp, isProcessed = false });
            }
            foreach(SoftwareDistributionPackage sdp in sdps)
            {
                IList<PrerequisiteGroup> groups = sdp.Prerequisites;
                foreach(PrerequisiteGroup g in groups)
                {
                    IList<Guid> ids = g.Ids;
                    foreach(Guid id in ids)
                    {
                        d[id].depend.AddLast(sdp.PackageId);
                    }
                }
                IList<Guid> bundledPackages = sdp.BundledPackages;
                foreach(Guid guid in bundledPackages)
                {
                    d[guid].depend.AddLast(sdp.PackageId);
                }
            }

            foreach(SoftwareDistributionPackage sdp in sdps)
            {
                SoftwareDistributionPackageAugment2 sdpAug2 = d[sdp.PackageId];
                if (!sdpAug2.isProcessed)
                {
                    DfsDeleteRecur(d, sdp, instruct, stats);
                }
            }
        }


        public ImportStats ImportFromSdp(VendorProfile vp, ImportInstructions instruct)
        {
            string s = Path.Combine(dirFinder.GetCabExtractOutputDir(), vp.ExtractOutputFolderName, "V2");
            ImportStats stats = new ImportStats();
            if (Directory.Exists(s))
            {
                var dirInfo = new DirectoryInfo(s);
                FileInfo[] files = dirInfo.GetFiles();
                stats.Total = files.Length;

                SqlConnection sqlCon = null;
                if (instruct.UpdateSusdbForVisibilityInConsole)
                {
                    sqlCon = new SqlConnection(ConnectionStr);
                    sqlCon.Open();
                }

                if (instruct.AsyncProcessEachPackage)
                {
                    int i = 0, batch = 1;
                    while (i < files.Length)
                    {
                        LinkedList<Task<bool>> tasks = new LinkedList<Task<bool>>();

                        for (; i < batch * batchSize && i < files.Length; i++)
                        {
                            if (instruct.PublishOrDelete)
                            {
                                FileInfo f = files[i];
                                SinglePackagePublishInstruction singlePkgInstruct = new SinglePackagePublishInstruction(vp, false, false, f.FullName, instruct.ArtificiallyMarshalDetectoid, instruct.UpdateSusdbForVisibilityInConsole, instruct.AsyncUpdateSusdb, sqlCon);
                                tasks.AddLast(ImportFromSingleSdp(f.FullName, true, singlePkgInstruct));
                            }
                            else
                            {
                                FileInfo f = files[i];
                                SinglePackagePublishInstruction singlePkgInstruct = new SinglePackagePublishInstruction(vp, false, false, f.FullName, instruct.ArtificiallyMarshalDetectoid, instruct.UpdateSusdbForVisibilityInConsole, instruct.AsyncUpdateSusdb, sqlCon);
                                tasks.AddLast(ImportFromSingleSdp(f.FullName, false, singlePkgInstruct));
                            }
                        }
                        Task.WaitAll(tasks.ToArray());
                        foreach (Task<bool> t in tasks)
                        {
                            if (t.Result)
                            {
                                stats.Success++;
                            }
                            else
                                stats.Failure++;
                        }
                        batch++;
                    }
                }
                else // Synchronous
                {
                    try
                    {
                        foreach (FileInfo file in files)
                        {
                            if (instruct.PublishOrDelete)
                            {
                                SinglePackagePublishInstruction singlePkgInstruct = new SinglePackagePublishInstruction(vp, false, false, file.FullName, instruct.ArtificiallyMarshalDetectoid, instruct.UpdateSusdbForVisibilityInConsole, instruct.AsyncUpdateSusdb, sqlCon);
                                Task<bool> t = ImportFromSingleSdp(file.FullName, true, singlePkgInstruct);
                                t.Wait();
                                if (t.Result)
                                { stats.Success++; }
                                else
                                {  stats.Failure++; }
                            }
                            else
                            {
                                SinglePackagePublishInstruction singlePkgInstruct = new SinglePackagePublishInstruction(vp, false, false, file.FullName, instruct.ArtificiallyMarshalDetectoid, instruct.UpdateSusdbForVisibilityInConsole, instruct.AsyncUpdateSusdb, sqlCon);
                                Task<bool> t = ImportFromSingleSdp(file.FullName, false, singlePkgInstruct);
                                t.Wait();
                                if (t.Result)
                                { stats.Success++; }
                                else
                                { stats.Failure++; }
                            }
                        }
                    }
                    catch
                    {
                        stats.Failure++;
                    }
                }
                sqlCon?.Close();
                return stats;
            }
            else
            {
                return stats;
            }
        }

        public async Task<bool> ImportFromSingleSdp(string sdpFilePath, bool publishOrDelete, SinglePackagePublishInstruction instruct)
        {
            try
            {
                SoftwareDistributionPackage sdp = new SoftwareDistributionPackage(sdpFilePath);
                
                if (publishOrDelete)
                {
                    Task<bool> t = PublishOnePackage(sdp, instruct);
                    await t;
                    return t.Result;
                }
                else
                {
                    return DeleteOnePackage(instruct.Vp, sdp);
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Fail to import SDP file, {file}\n", sdpFilePath);
                return false;
            }
        }

        private async Task<bool> UpdateDatabaseAsync(string packageId, SqlConnection sqlConnection)
        {
            try
            {
                string CommandText = "UPDATE [SUSDB].[dbo].[tbUpdate] SET [IsLocallyPublished] = 0 WHERE [UpdateID] = '" + packageId + "'";

                SqlCommand sqlCommand = new SqlCommand(CommandText, sqlConnection);

                await sqlCommand.ExecuteNonQueryAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fail to udpate database, package ID: {pkgid}\n", packageId);
                return false;
            }
        }

        private bool UpdateDatabase(string packageId, SqlConnection sqlConnection)
        {
            try
            {
                string CommandText = "UPDATE [SUSDB].[dbo].[tbUpdate] SET [IsLocallyPublished] = 0 WHERE [UpdateID] = '" + packageId + "'";

                SqlCommand sqlCommand = new SqlCommand(CommandText, sqlConnection);

                sqlCommand.ExecuteNonQuery();

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fail to udpate database, package ID: {pkgid}\n", packageId);
                return false;
            }
        }
    }
}
