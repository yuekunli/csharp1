using System;
using System.Collections.Generic;
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
            SevenZipBase.SetLibraryPath(@"C:\Program Files (x86)\7-Zip\7z.dll");
            using (SevenZipExtractor extractor = new SevenZipExtractor(@"C:\Users\YuekunLi\Downloads\cab_import_test\DellSDPCatalogPC.cab"))
            {
                for (var i = 0; i < extractor.ArchiveFileData.Count; i++)
                {
                    Console.WriteLine(extractor.ArchiveFileData[i].FileName);
                    extractor.ExtractFiles(outputDir, extractor.ArchiveFileData[i].Index);
                }
            }
        }
    }
}
