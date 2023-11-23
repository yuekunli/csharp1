using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DriverCatalogImporter
{
    internal class FileComparer
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        private readonly SHA256 sha256;

        private readonly ILogger logger;

        private readonly IDirFinder dirFinder;

        public FileComparer(ILogger _logger, IDirFinder _dirFinder)
        {
            sha256 = SHA256.Create();
            logger = _logger;
            this.dirFinder = _dirFinder;
        }

        public async Task<bool> IsSame(VendorProfile vp)
        {
            Task<bool> t = Task.Run(() =>
            {
                string oldFilePath = Path.Combine(dirFinder.GetOldCabFileDir(), vp.CabFileName);
                string newFilePath = Path.Combine(dirFinder.GetNewCabFileDir(), vp.CabFileName);
                if (!File.Exists(oldFilePath) && File.Exists(newFilePath))
                {
                    logger.LogInformation("[{VendorName}] : old cab file not exist", vp.Name);
                    return false;
                }
                if (!File.Exists(newFilePath))
                {
                    logger.LogError("[{VendorName}] : new cab file not exist", vp.Name);
                    throw new Exception($"[{vp.Name}] : new cab file not exist");
                }
                using (FileStream f1 = File.OpenRead(oldFilePath))
                {
                    using (FileStream f2 = File.OpenRead(newFilePath))
                    {
                        try
                        {
                            f1.Position = 0;
                            f2.Position = 0;
                            byte[] b1 = sha256.ComputeHash(f1);
                            byte[] b2 = sha256.ComputeHash(f2);
                            return (b1.Length == b2.Length) && (memcmp(b1, b2, b1.Length) == 0);
                        }
                        catch (IOException e)
                        {
                            logger.LogError(e, "[{vendorname}] : IO exception while computing hash\n", vp.Name);
                            return false;
                        }
                        catch (UnauthorizedAccessException e)
                        {
                            logger.LogError(e, "[{vendorname}] : denied access while computing hash\n", vp.Name);
                            return false;
                        }
                    }
                }
            });
            await t;
            return t.Result;
        }
    }
}
