using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    internal class ImportInstructions
    {
        public bool AsyncProcessEachPackage { get; set; }
        public bool UpdateSusdbForVisibilityInConsole { get; set; }

        public ImportInstructions(bool asyncProcessEachPackage, bool updateSusdbForVisibilityInConsole)
        {
            AsyncProcessEachPackage = asyncProcessEachPackage;
            UpdateSusdbForVisibilityInConsole = updateSusdbForVisibilityInConsole;
        }
    }
}
