using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    internal interface IImporter
    {
        Task<ImportStats> ImportFromXml(VendorProfile vp, ImportInstructions instruct);
    }
}
