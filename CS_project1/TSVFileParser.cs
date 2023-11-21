using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_project1
{
    internal class TSVFileParser
    {
        public static void parseFile()
        {
            string filePath = @"C:\Temp\DriverCatalog.cfg";
            var entries = System.IO.File.ReadAllLines(filePath);

            foreach (var entry in entries)
            {
                var parts = entry.Split('\t');
                string k = parts[0];
                string v = parts[1];
                {
                    Console.WriteLine(k);
                    Console.WriteLine(v);
                    if (k.Equals("IsProd"))
                    {
                        if (v.Equals("true"))
                        {
                            Console.WriteLine("This is a production run");
                        }
                        else
                        {
                            Console.WriteLine("This is an experiment run");
                        }
                    }
                    if (k.Equals("VendorProfileFilePath"))
                    {
                        Console.WriteLine("Override vendor profile with {0}", v);
                    }
                }
            }
        }
    }
}
