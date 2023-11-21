using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System;
using System.IO;
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
            sha256 = SHA256.Create(); // if we can't create hash algorithm instance, let the exception bubble up.
            logger = _logger;
            this.dirFinder = _dirFinder;
        }

        public bool IsSame(VendorProfile vp)
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
                return true; // TODO: use a better return code
            }
            FileStream f1 = File.OpenRead(oldFilePath);
            FileStream f2 = File.OpenRead(newFilePath);
            try
            {
                f1.Position= 0;
                f2.Position= 0;
                byte[] b1 = sha256.ComputeHash(f1);
                byte[] b2 = sha256.ComputeHash(f2);
                f1.Close();
                f2.Close();
                return (b1.Length == b2.Length) && (memcmp(b1, b2, b1.Length) == 0);
            }
            catch (IOException e)
            {
                f1.Close();
                f2.Close();
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                f1.Close();
                f2.Close();
                return false;
            }
        }
    }
}
