using Microsoft.Extensions.Logging;
using SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    internal class CabExtractor
    {
        private ILogger logger;
        private IDirFinder dirFinder;
        public CabExtractor(ILogger _logger, IDirFinder _dirFinder) 
        {
            this.logger = _logger;
            dirFinder = _dirFinder;
            try
            {
                SevenZipBase.SetLibraryPath(Path.Combine(AppContext.BaseDirectory, "7z64.dll"));
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Fail to find 64-bit 7z.dll, which is necessary to extract cabinet files\n");
                throw new Exception("Fail to find 64-bit 7z.dll");
            }
        }
        public async Task<bool> ExtractXml(VendorProfile vp)
        {
            string cabFilePath = Path.Combine(dirFinder.GetNewCabFileDir(), vp.CabFileName);
            string xmlFileName = Path.ChangeExtension(vp.CabFileName, ".xml");
            
            string extractedXmlFilePath = Path.Combine(dirFinder.GetCabExtractOutputDir(), xmlFileName);
            logger.LogInformation("[{vn}] : cab file path: {path}", vp.Name, cabFilePath);
            try
            {
                using (SevenZipExtractor extractor = new SevenZipExtractor(cabFilePath))
                {
                    int index = -1;
                    foreach (ArchiveFileInfo archiveFileDatum in extractor.ArchiveFileData)
                    {
                        if (archiveFileDatum.FileName.EndsWith("xml") && !archiveFileDatum.IsDirectory)
                        {
                            index = archiveFileDatum.Index;
                            break;
                        }
                    }

                    if (index == -1)
                    {
                        logger.LogError("[{vendorname}] : No XML file in cab");
                        return false;
                    }

                    logger.LogDebug("[{vendorname}] : start extracting XML file", vp.Name);
                    using (FileStream fs = File.Create(extractedXmlFilePath))
                    {
                        await extractor.ExtractFileAsync(index, fs);
                    }
                    logger.LogDebug("[{vendorname}] : done extracting XML file", vp.Name);
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{vendorname}] : Fail to extract XML file\n", vp.Name);
                return false;
            }
        }

        public async Task<bool> ExtractV2Sdp(VendorProfile vp)
        {
            string cabFilePath = Path.Combine(dirFinder.GetNewCabFileDir(), vp.CabFileName);
            string outputDir = Path.Combine(dirFinder.GetCabExtractOutputDir(), vp.ExtractOutputFolderName, "V2");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            try
            {
                using (SevenZipExtractor extr = new SevenZipExtractor(cabFilePath))
                {
                    logger.LogDebug("[{vn}] : start extracting SDP files in V2 folder", vp.Name);

                    List<Task> tasks = new List<Task>();
                    for (var i = 0; i < extr.ArchiveFileData.Count; i++)
                    {
                        if (extr.ArchiveFileData[i].FileName.StartsWith("V2", StringComparison.CurrentCultureIgnoreCase))
                        {
                            string outputFilePath = Path.Combine(outputDir, extr.ArchiveFileData[i].FileName.Substring(3));
                            using (var fs= File.Create(outputFilePath))
                            {
                                var t = extr.ExtractFileAsync(extr.ArchiveFileData[i].Index, fs);
                                tasks.Add(t);
                            }
                        }
                    }
                    await Task.WhenAll(tasks);
                }
                logger.LogDebug("[{vn}] : Done extracting SDP files in V2 folder", vp.Name);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{vn}] : Fail to extract SDP files in V2 folder\n", vp.Name);
                return false;
            }
        }

        public bool DeleteXml(VendorProfile vp)
        {
            try
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
                string extractedXmlFilePath = Path.Combine(dirFinder.GetCabExtractOutputDir(), xmlFileName);
                if (File.Exists(extractedXmlFilePath))
                {
                    File.Delete(extractedXmlFilePath);
                }
                return true;
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "[{vendorname}] : Fail to delete temporary XML file\n", vp.Name);
                return false;
            }
        }
    }
}
