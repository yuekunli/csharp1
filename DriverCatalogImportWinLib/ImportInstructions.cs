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
        public bool AsyncUpdateSusdb {  get; set; }
        public bool PublishOrDelete { get; set; }
        public bool ArtificiallyMarshalDetectoid { get; set; }
        public bool OnlyOneDetectoidInLenovo {  get; set; }

        public ImportInstructions(bool asyncProcessEachPackage, bool updateSusdbForVisibilityInConsole, bool asyncUpdateSusdb, bool publishOrDelete, bool artificiallyMarshalDetectoid, bool onlyOneDetectoidInLenovo)
        {
            AsyncProcessEachPackage = asyncProcessEachPackage;
            UpdateSusdbForVisibilityInConsole = updateSusdbForVisibilityInConsole;
            AsyncUpdateSusdb = asyncUpdateSusdb;
            PublishOrDelete = publishOrDelete;
            ArtificiallyMarshalDetectoid = artificiallyMarshalDetectoid;
            OnlyOneDetectoidInLenovo = onlyOneDetectoidInLenovo;
        }
    }
}
