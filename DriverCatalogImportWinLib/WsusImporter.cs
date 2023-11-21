using Microsoft.Extensions.Logging;
using Microsoft.UpdateServices.Administration;
using System.Data.SqlClient;
using System.Xml;
using System.Xml.XPath;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace DriverCatalogImporter
{
    internal class WsusImporter : IImporter
    {
        private IUpdateServer wsus;

        private ILogger logger;

        private IDirFinder dirFinder;
        public WsusImporter(ILogger _logger, IDirFinder _dirFinder)
        {
            wsus = AdminProxy.GetUpdateServer();
            logger = _logger;
            this.dirFinder = _dirFinder;
        }

        public bool ImportFromXml(VendorProfile vp)
        {
            string xmlFileName = Path.ChangeExtension(vp.CabFileName, ".xml");
            string xmlFilePath = Path.Combine(dirFinder.GetCabExtractOutputDir(), xmlFileName);
            try
            {
                //IUpdateServer wsus = AdminProxy.GetUpdateServer();
                System.Collections.Concurrent.ConcurrentQueue<SoftwareDistributionPackage> extractedSdps = new System.Collections.Concurrent.ConcurrentQueue<SoftwareDistributionPackage>();

                XmlNamespaceManager nsmgr = new XmlNamespaceManager(new NameTable());
                nsmgr.AddNamespace("", "http://www.w3.org/2001/XMLSchema");
                nsmgr.AddNamespace("smc", "http://schemas.microsoft.com/sms/2005/04/CorporatePublishing/SystemsManagementCatalog.xsd");

                XmlDocument xmlDoc = new XmlDocument();
                
                try
                {
                    xmlDoc.Load(xmlFilePath);

                    var xmlNodeList = xmlDoc.SelectNodes("smc:SystemsManagementCatalog/smc:SoftwareDistributionPackage", nsmgr);

                    Parallel.ForEach<XmlNode>(Enumerable.Cast<XmlNode>(xmlNodeList), node =>
                    {
                        IXPathNavigable nav = node;
                        XPathNavigator ntor = nav.CreateNavigator();
                        extractedSdps.Enqueue(new SoftwareDistributionPackage(ntor));
                    });
                }
                catch (Exception ex)
                {

                }

                foreach(SoftwareDistributionPackage sdp in extractedSdps)
                {
                    string tmpSdpFilePath = Path.Combine(dirFinder.GetTmpSdpFileDir(), sdp.PackageId.ToString());
                    tmpSdpFilePath = Path.ChangeExtension(tmpSdpFilePath, ".sdp");

                    sdp.Save(tmpSdpFilePath);

                    IPublisher publisher = wsus.GetPublisher(tmpSdpFilePath);
                    publisher.MetadataOnly = true;
                    try
                    {
                        publisher.PublishPackage((string)null, (string)null, (string)null);
                    }
                    catch (Exception ex)
                    {
                        publisher.RevisePackage();
                    }
                    UpdateDatabase(sdp.PackageId.ToString());
                    File.Delete(tmpSdpFilePath);
                }
                return true;
            }
            catch(Exception ex)
            {
                return false;
            }
        }

        public bool ImportFromSdp(string sdpFilePath)
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
                UpdateDatabase(sdpId);
                return true;
            }
            catch(Exception ex)
            {
                return false;
            }
        }

        /**
         * What is this database?
         */
        private bool UpdateDatabase(string packageId)
        {
            try
            {
                // Define the server name and database name for the SQL connection.
                string ServerName = "\\\\.\\pipe\\Microsoft##WID\\tsql\\query";
                string DataBaseName = "SUSDB";

                // Log the start of the database connection process.
                

                // Create a new SQL connection.
                SqlConnection sqlConnection = new SqlConnection();

                // Set the connection string for the SQL connection using the server and database names.
                sqlConnection.ConnectionString = string.Format("Server={0};Database={1};Integrated Security=sspi;", (object)ServerName, (object)DataBaseName);

                // Open the SQL connection.
                sqlConnection.Open();

                // Create a new SQL command.
                SqlCommand sqlCommand = new SqlCommand();

                // Set the connection for the SQL command to the previously opened SQL connection.
                sqlCommand.Connection = sqlConnection;

                // Set the command text to update the IsLocallyPublished field for the given ID.
                sqlCommand.CommandText = "UPDATE [SUSDB].[dbo].[tbUpdate] SET [IsLocallyPublished] = 0 WHERE [UpdateID] = '" + packageId + "'";

                // Execute the SQL command.
                sqlCommand.ExecuteNonQuery();

                // Return true indicating successful database update.
                return true;
            }
            catch (Exception ex)
            {
                // Return false indicating the database update was unsuccessful.
                return false;
            }
        }
    }
}
