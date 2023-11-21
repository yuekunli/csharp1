using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CS_project1
{
    internal class FileHasher
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        static public void getHash()
        {
            string path1 = @"C:\\Users\\YuekunLi\\Downloads\\LenovoUpdatesCatalog2v2.cab";
            string path2 = @"C:\\Users\\YuekunLi\\Downloads\\b\\DellSDPCatalogPC.cab";

            byte[] hashValue1 = null;
            byte[] hashValue2 = null;

            using (SHA256 sha256 = SHA256.Create())
            {
                using (FileStream fs = File.OpenRead(path1))
                {
                    try
                    {
                        fs.Position = 0;
                        hashValue1 = sha256.ComputeHash(fs);

                    }
                    catch (IOException e)
                    {
                        Console.WriteLine($"I/O Exception: {e.Message}");
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        Console.WriteLine($"Access Exception: {e.Message}");
                    }
                }

                using (FileStream fs = File.OpenRead(path2))
                {
                    try
                    {
                        fs.Position = 0;
                        hashValue2 = sha256.ComputeHash(fs);

                    }
                    catch (IOException e)
                    {
                        Console.WriteLine($"I/O Exception: {e.Message}");
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        Console.WriteLine($"Access Exception: {e.Message}");
                    }
                }
            }

            StringBuilder sb = new StringBuilder();

            foreach (byte b in hashValue1)
            {
                sb.Append(b.ToString("x2"));
            }
            Console.WriteLine(sb.ToString());

            sb.Clear();
            foreach (byte b in hashValue2)
            {
                sb.Append(b.ToString("x2"));
            }
            Console.WriteLine(sb.ToString());

            int isEqual = memcmp(hashValue1, hashValue2, hashValue1.Length);
            Console.WriteLine($"is equal: {isEqual}");
        }
    }
}
