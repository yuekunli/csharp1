﻿using System.IO.Compression;

namespace CS_project1
{
    internal class ExtractZipFile
    {
        public ExtractZipFile() { }
        static public void extract()
        {
            string zipPath = @"C:\\Users\\YuekunLi\\Downloads\\aa.zip";

            //Console.WriteLine("Provide path where to extract the zip file:");
            //string extractPath = Console.ReadLine();
            string extractPath = @"C:\\Users\\YuekunLi\\Downloads\\a\\";

            // Normalizes the path.
            extractPath = Path.GetFullPath(extractPath);

            // Ensures that the last character on the extraction path
            // is the directory separator char.
            // Without this, a malicious zip file could try to traverse outside of the expected
            // extraction path.
            if (!extractPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                extractPath += Path.DirectorySeparatorChar;

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        // Gets the full path to ensure that relative segments are removed.
                        string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));

                        // Ordinal match is safest, case-sensitive volumes can be mounted within volumes that
                        // are case-insensitive.
                        if (destinationPath.StartsWith(extractPath, StringComparison.Ordinal))
                            entry.ExtractToFile(destinationPath);
                    }
                }
            }
        }
    }
}
