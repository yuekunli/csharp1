using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace NetFrameworkConsoleApp1
{
    internal class XmlParser
    {
        public static void TryTheParser()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load("2books.txt");

            //Get and display all the book titles.
            XmlElement root = doc.DocumentElement;
            XmlNodeList elemList = root.GetElementsByTagName("title");

            System.Collections.Concurrent.ConcurrentQueue<string> extractedTitles = new System.Collections.Concurrent.ConcurrentQueue<string>();

            Parallel.ForEach<XmlNode>(Enumerable.Cast<XmlNode>(elemList), node =>
            {
                extractedTitles.Enqueue(node.InnerText);
            });

            foreach(string s in extractedTitles)
            {
                Console.WriteLine(s);
            }
        }
    }
}
