using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SevenZip;

namespace NetFrameworkConsoleApp1
{
    internal class CabExtractor
    {
        public static void ExtractCabFile()
        {
            string outputDir = @"C:\Users\YuekunLi\Downloads\cab_import_test\a\";
            SevenZipBase.SetLibraryPath(@"C:\Users\YuekunLi\Downloads\7z64.dll"); //@"C:\Program Files (x86)\7-Zip\7z.dll"
            using (SevenZipExtractor extractor = new SevenZipExtractor(@"C:\Users\YuekunLi\Downloads\cab_import_test\DellSDPCatalogPC.cab"))
            {
                for (var i = 0; i < extractor.ArchiveFileData.Count; i++)
                {
                    Console.WriteLine(extractor.ArchiveFileData[i].FileName);
                    extractor.ExtractFiles(outputDir, extractor.ArchiveFileData[i].Index);
                }
            }
        }

        public static async Task ExtractCabFile_IterateArchiveToFindXml()
        {
            string outputDir = @"C:\temp\";
            SevenZipBase.SetLibraryPath(@"C:\temp\7z64.dll");
            using (SevenZipExtractor extractor = new SevenZipExtractor(@"C:\temp\HpCatalogForSms.latest.cab"))
            {
                int index = -1;
                string filename = "";
                foreach (ArchiveFileInfo f in extractor.ArchiveFileData)
                {
                    if (f.FileName.EndsWith("xml") && !f.IsDirectory)
                    {
                        index = f.Index;
                        filename = f.FileName;
                        break;
                    }
                }

                if (index == -1)
                {
                    Console.WriteLine("not find XML file");
                    return;
                }
                string outputPath = Path.Combine(outputDir, filename);
                using (FileStream fs = File.Create(outputPath))
                {
                    await extractor.ExtractFileAsync(index, fs);
                }
            }

            return;
        }
    }
}
