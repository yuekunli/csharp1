using Microsoft.Extensions.Logging;
using Microsoft.UpdateServices.Administration;
using System.Data.SqlClient;
using System.Xml;
using System.Xml.XPath;

namespace DriverCatalogImporter
{
    internal class WsusImporter : IImporter
    {
        private readonly IUpdateServer wsus;
        private readonly ILogger logger;
        private readonly IDirFinder dirFinder;
        private readonly string ServerName = "\\\\.\\pipe\\Microsoft##WID\\tsql\\query";
        private readonly string DataBaseName = "SUSDB";
        private readonly string ConnectionStr;

        public WsusImporter(ILogger _logger, IDirFinder _dirFinder)
        {
            wsus = AdminProxy.GetUpdateServer();
            ConnectionStr = string.Format("Server={0};Database={1};Integrated Security=sspi;Pooling=true", ServerName, DataBaseName);
            logger = _logger;
            dirFinder = _dirFinder;
        }

        public async Task<bool> ImportFromXml(VendorProfile vp)
        {
            string xmlFileName = Path.ChangeExtension(vp.CabFileName, ".xml");
            string xmlFilePath = Path.Join(dirFinder.GetCabExtractOutputDir(), xmlFileName);
            if (File.Exists(xmlFilePath))
            {
                logger.LogDebug("[{vn}] : Start parsing and importing XML file", vp.Name);
            }
            else
            {
                logger.LogError("[{vn}] : XML file does not exist", vp.Name);
                return false;
            }

            //IUpdateServer wsus = AdminProxy.GetUpdateServer();
            System.Collections.Concurrent.ConcurrentQueue<SoftwareDistributionPackage> extractedSdps = new();

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(new NameTable());
            try
            {
                nsmgr.AddNamespace("", "http://www.w3.org/2001/XMLSchema");
                nsmgr.AddNamespace("smc", "http://schemas.microsoft.com/sms/2005/04/CorporatePublishing/SystemsManagementCatalog.xsd");
            }
            catch (Exception e) 
            {
                logger.LogError(e, "[{vn}] : Fail to load XML namespace", vp.Name);
                return false;
            }

            XmlDocument xmlDoc = new XmlDocument();
                
            try
            {
                xmlDoc.Load(xmlFilePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{vn}] : Fail to load XML file", vp.Name);
                return false;
            }

            try
            {
                var xmlNodeList = xmlDoc.SelectNodes("smc:SystemsManagementCatalog/smc:SoftwareDistributionPackage", nsmgr);
                    
                if (xmlNodeList != null)
                {
                    //var source = xmlNodeList.Cast<XmlNode>().ToList();

                    Parallel.ForEach(Enumerable.Cast<XmlNode>(xmlNodeList), node =>
                    {
                        IXPathNavigable nav = node;
                        XPathNavigator ntor = nav.CreateNavigator();
                        extractedSdps.Enqueue(new SoftwareDistributionPackage(ntor));
                    });
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "[{vn}] : Fail to find relevant nodes in XML file", vp.Name);
                return false;
            }

            using SqlConnection sqlCon = new SqlConnection(ConnectionStr);
            sqlCon.Open();
            foreach(SoftwareDistributionPackage sdp in extractedSdps)
            {
                string tmpSdpFilePath = Path.Join(dirFinder.GetTmpSdpFileDir(), sdp.PackageId.ToString());
                tmpSdpFilePath = Path.ChangeExtension(tmpSdpFilePath, ".sdp");

                sdp.Save(tmpSdpFilePath);
                logger.LogDebug("[{vn}] : saved temporary sdp file", vp.Name);

                IPublisher publisher = wsus.GetPublisher(tmpSdpFilePath);
                publisher.MetadataOnly = true;
                try
                {
                    publisher.PublishPackage((string)null, (string)null, (string)null); // TODO: fix this warning
                    logger.LogDebug("[{vn}] : published package [{pkgid}]", vp.Name, sdp.PackageId.ToString());
                }
                catch
                {
                    logger.LogInformation("[{vn}] : Fail to publish package, may be due to conflict, try revise package", vp.Name);
                    try
                    {
                        publisher.RevisePackage();
                        logger.LogInformation("[{vn}] : revised package [{pkgid}]", vp.Name, sdp.PackageId.ToString());
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[{vn}] : Fail to revise package, this is the final attempt to publish the package", vp.Name);
                    }
                }
                var t = UpdateDatabase(sdp.PackageId.ToString(), sqlCon);
                await t;
                if (!t.Result)
                {
                    logger.LogError("[{vn}] : Fail to update database", vp.Name);
                }
                File.Delete(tmpSdpFilePath);
                logger.LogDebug("[{vn}] : Deleted temporary sdp file", vp.Name);
            }
            return true;
        }

        public async Task<bool> ImportFromSdp(VendorProfile vp)
        {
            string s = Path.Join(dirFinder.GetCabExtractOutputDir(), vp.ExtractOutputFolderName, "V2");
            if (Directory.Exists(s))
            {
                var dirInfo = new DirectoryInfo(s);
                FileInfo[] files = dirInfo.GetFiles();
                using SqlConnection sqlCon = new SqlConnection(ConnectionStr);
                sqlCon.Open();
                foreach (FileInfo file in files)
                {
                    await ImportFromSdp(file.FullName, sqlCon);
                }
                sqlCon.Close();
            }
            return true;
        }
        private async Task<bool> ImportFromSdp(string sdpFilePath, SqlConnection sqlConnection)
        {
            try
            {
                IPublisher publisher = wsus.GetPublisher(sdpFilePath);
                publisher.MetadataOnly = true;
                try
                {
                    publisher.PublishPackage(null, null, null);
                }
                catch (Exception ex)
                {
                    publisher.RevisePackage();
                }
                string sdpId = Path.GetFileNameWithoutExtension(sdpFilePath);
                await UpdateDatabase(sdpId, sqlConnection);
                return true;
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Fail to import SDP file, {file}", sdpFilePath);
                return false;
            }
        }

        private async Task<bool> UpdateDatabase(string packageId, SqlConnection sqlConnection)
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
                logger.LogError(ex, "Fail to udpate database, package ID: {pkgid}", packageId);
                return false;
            }
        }
    }
}
