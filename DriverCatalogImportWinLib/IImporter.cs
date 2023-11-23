using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    internal interface IImporter
    {
        Task<bool> ImportFromXml(VendorProfile vp);
    }
}
