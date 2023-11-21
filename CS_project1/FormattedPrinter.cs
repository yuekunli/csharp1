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
    }
}
