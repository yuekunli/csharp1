using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    internal interface IImporter
    {
        ImportStats ImportFromXml(VendorProfile vp, ImportInstructions instruct);
    }
}
