using System.Text;

namespace CS_project1
{
    internal class FlagFileWriter
    {
        static public void writeFlagFile()
        {
            string path = @"C:\\Users\\YuekunLi\\Downloads\\flag.txt";

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using (FileStream fs = File.Create(path))
            {
                //AddText(fs, "This is some text");
                //AddText(fs, "This is some more text,");
                //AddText(fs, "\r\nand this is on a new line");
                //AddText(fs, "\r\n\r\nThe following is a subset of characters:\r\n");
                AddText(fs, "go right ahead\r\n");

                for (int i = 1; i < 120; i++)
                {
                    AddText(fs, Convert.ToChar(i).ToString());
                }
            }
        }
        private static void AddText(FileStream fs, string value)
        {
            byte[] info = new UTF8Encoding().GetBytes(value);
            fs.Write(info, 0, info.Length);
        }
    }
}
