using SevenZip;

namespace CS_project1
{
    internal class CabFileWorker
    {
        private const string outputDir = @"C:\Users\YuekunLi\Downloads\cab_import_test\a\";
        static public void extract()
        {
            SevenZipExtractor.SetLibraryPath("7z.dll"); //  @"C:\Program Files\7-Zip\7z.dll"
            SevenZipExtractor extractor = new SevenZipExtractor(@"C:\Users\YuekunLi\Downloads\cab_import_test\DellSDPCatalogPC.cab");
            for (var i = 0; i < extractor.ArchiveFileData.Count; i++)
            {
                Console.WriteLine(extractor.ArchiveFileData[i].FileName);
                extractor.ExtractFiles(outputDir, extractor.ArchiveFileData[i].Index);
            }
        }
    }
}
