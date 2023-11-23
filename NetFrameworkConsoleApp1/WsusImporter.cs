using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UpdateServices.Administration;

namespace NetFrameworkConsoleApp1
{
    internal class WsusImporter
    {
        public static void test()
        {
            IUpdateServer s = AdminProxy.GetUpdateServer();
            Console.WriteLine(s.ToString());
        }

    }
}
