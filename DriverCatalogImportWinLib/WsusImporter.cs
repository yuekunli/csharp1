using Microsoft.Extensions.Logging;
using Microsoft.UpdateServices.Administration;
using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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

        public WsusImporter(ILogger _logger, IDirFinder _dirFinder) : this (_logger, _dirFinder, null) { }
        public WsusImporter(ILogger _logger, IDirFinder _dirFinder, IPEndPoint _wsusEndPoint)
        {
            if (_wsusEndPoint != null)
            {
                if (_wsusEndPoint.Port != 0)
                {
                    wsus = AdminProxy.GetUpdateServer(_wsusEndPoint.Address.ToString(), false, _wsusEndPoint.Port);
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

        public async Task<bool> ImportFromXml(VendorProfile vp)
        {
            string xmlFileName;
            if (!vp.Name.Equals("HP", StringComparison.CurrentCultureIgnoreCase))
            {
                xmlFileName = Path.ChangeExtension(vp.CabFileName, ".xml");
            }
            else
            {
                xmlFileName = Path.ChangeExtension(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(vp.CabFileName)), ".xml");
            }
            string xmlFilePath = Path.Combine(dirFinder.GetCabExtractOutputDir(), xmlFileName);

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
                return false;
            }

            XmlDocument xmlDoc = new XmlDocument();
                
            try
            {
                xmlDoc.Load(xmlFilePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{vn}] : Fail to load XML file\n", vp.Name);
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
                logger.LogError(e, "[{vn}] : Fail to find relevant nodes in XML file\n", vp.Name);
                return false;
            }

            SqlConnection sqlCon = new SqlConnection(ConnectionStr);
            sqlCon.Open();
            foreach(SoftwareDistributionPackage sdp in extractedSdps)
            {
                string tmpSdpFilePath = Path.Combine(dirFinder.GetTmpSdpFileDir(), sdp.PackageId.ToString());
                tmpSdpFilePath = Path.ChangeExtension(tmpSdpFilePath, ".sdp");
                bool success= false;
                sdp.Save(tmpSdpFilePath);
                logger.LogDebug("[{vn}] : saved temporary sdp file {pkgid}", vp.Name, sdp.PackageId.ToString());

                IPublisher publisher = wsus.GetPublisher(tmpSdpFilePath);
                publisher.MetadataOnly = true;
                try
                {
                    wsus.GetUpdate(new UpdateRevisionId(sdp.PackageId, 0));
                    logger.LogDebug("[{vn}] : get udpate {pkgid}", vp.Name, sdp.PackageId.ToString());
                }
                catch
                {
                    logger.LogDebug("[{vn}] : not get udpate {pkgid}", vp.Name, sdp.PackageId.ToString());
                }
                try
                {
                    publisher.PublishPackage(null, null);
                    logger.LogDebug("[{vn}] : Success, publish package {pkgid}", vp.Name, sdp.PackageId.ToString());
                    success = true;
                }
                catch (Exception e1)
                {
                    logger.LogError("[{vn}] : Fail, publish package {pkgid}, {error}\n", vp.Name, sdp.PackageId.ToString(), e1.Message);

                    if (e1.Message.Contains("the following Prerequisites haven't been published yet"))
                    {
                        File.Delete(tmpSdpFilePath);
                        logger.LogTrace("[{vn}] : Deleted temporary sdp file", vp.Name);
                    }
                    else
                    {
                        logger.LogInformation("[{vn}] : Fail, publish package {pkgid}, try revise package", vp.Name, sdp.PackageId.ToString());
                        try
                        {
                            publisher.RevisePackage();
                            logger.LogInformation("[{vn}] : Success, revise package {pkgid}", vp.Name, sdp.PackageId.ToString());
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError("[{vn}] : Fail, revise package {pkgid}, no more attempt,  {error}\n", vp.Name, sdp.PackageId.ToString(), ex.Message);
                            File.Delete(tmpSdpFilePath);
                            logger.LogTrace("[{vn}] : Deleted temporary sdp file", vp.Name);
                        }
                    }
                }
                if (success)
                {
                    var t = UpdateDatabase(sdp.PackageId.ToString(), sqlCon);
                    await t;
                    if (!t.Result)
                    {
                        logger.LogError("[{vn}] : Fail, update database", vp.Name);
                    }
                    File.Delete(tmpSdpFilePath);
                    logger.LogTrace("[{vn}] : Deleted temporary sdp file", vp.Name);
                }
            }
            sqlCon.Close();
            return true;
        }

        public async Task<bool> ImportFromSdp(VendorProfile vp)
        {
            string s = Path.Combine(dirFinder.GetCabExtractOutputDir(), vp.ExtractOutputFolderName, "V2");
            if (Directory.Exists(s))
            {
                var dirInfo = new DirectoryInfo(s);
                FileInfo[] files = dirInfo.GetFiles();
                SqlConnection sqlCon = new SqlConnection(ConnectionStr);
                sqlCon.Open();
                try
                {
                    foreach (FileInfo file in files)
                    {
                        await ImportFromSdp(file.FullName, sqlCon);
                    }
                }
                catch (Exception ex)
                {
                    sqlCon.Close();
                    logger.LogError(ex, "[{vn}] : Fail to import SDP files\n", vp.Name);
                    return false;
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
                catch
                {
                    publisher.RevisePackage();
                }
                string sdpId = Path.GetFileNameWithoutExtension(sdpFilePath);
                await UpdateDatabase(sdpId, sqlConnection);
                return true;
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Fail to import SDP file, {file}\n", sdpFilePath);
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
                logger.LogError(ex, "Fail to udpate database, package ID: {pkgid}\n", packageId);
                return false;
            }
        }
    }
}
