using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_project1
{
    internal class FormattedPrinter
    {
        internal class Profile
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public bool IsDefault { get; set; }

            public Profile(string _name, string _des, bool _isDefault) {
                Name = _name;
                Description = _des;
                IsDefault = _isDefault;
            }
        }

        private static Profile[] pp = {new Profile("Dell", "abcdksoloiudfdoielsmdjf", true),
        new Profile("Lenovo", "aaaaaaaaaaaa", false),
        new Profile("HP", "jlskdjfdkjf", true)
        };

        public static void print()
        {
            int width = 35;

            string formatS = "{0,-6} {1,-" + width + "} {2,-12}";

            Console.WriteLine(formatS, "Name", "Description", "IsDefault");
            foreach (Profile p in pp) {
                Console.WriteLine(formatS, p.Name, p.Description, p.IsDefault);
            }
        }

        public static void Print2()
        {
            string name = "Lenovo";
            string url = @"https://download.lenovo.com/luc/v2/LenovoUpdatesCatalog2v2.cab";
            DateTime t = DateTime.Now;

            using (StreamWriter sw = File.CreateText(@"C:\Temp\a.txt"))
            {
                sw.WriteLine("{0,-10}{1,-80}{2,-100}", name, url, t.ToString());
            }
        }

        public static void Read2()
        {
            var entries = File.ReadAllLines(@"C:\Temp\a.txt");
            foreach (var entry in entries)
            {
                var parts = entry.Split();
                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part)) continue;
                    Console.WriteLine($"{part.Trim()}");
                }
                DateTime t = DateTime.Parse(parts[^3] + " " + parts[^2] + " " + parts[^1]);
                Console.WriteLine(t.ToString());
            }
        }
    }
}
