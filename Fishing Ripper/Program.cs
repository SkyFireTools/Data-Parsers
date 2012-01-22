using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

using dict = System.Collections.Generic.Dictionary<string, object>;
namespace FishingRipper
{
    class Program
    {
        static void Main(string[] args)
        {

            StreamWriter outp = File.CreateText("fish_loot.sql");
            for (uint i = 0; i != 5450; i++)
            {
                UInt32 zoneId = i;
                string url = "http://www.wowhead.com/zone=" + i + "#fishing";

                List<string> content;
                try
                {
                    content = ReadPage(url);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Zone Id " + zoneId + " Doesn't exist (" + e.Message + ")");
                    continue;
                }

                Regex r = new Regex(@"new Listview\(\{template: 'item', id: 'fishing'.*data: (\[.+\])\}\);");
                foreach (string line in content)
                {
                    Match m = r.Match(line);
                    if (!m.Success)
                    {
                        continue;
                    }
                    // found our line
                    var json = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
                    string data = m.Groups[1].Captures[0].Value;
                    data = data.Replace("[,", "[0,");   // otherwise deserializer complains
                    object[] fishs = (object[])json.DeserializeObject(data);
                    foreach (dict fishInfo in fishs)
                    {
                        try
                        {
                            int id = (int)fishInfo["id"];
                            int maxcount = 1;
                            int pct = 0;
                            string name = "";
                            //stack = (int)fishInfo["stack"]; this is array idk do it too :/
                            if (fishInfo.ContainsKey("name"))
                                name = (string)fishInfo["name"];
                            // todo, figure out extended cost from honor cost
                            outp.WriteLine("replace into `fishing_loot_template` values ( '{0}', '{1}', '{2}', '{3}', '{4}', '{5}' , '{6}'); -- {7}",
                                                                    zoneId, id, pct, 2, 0, 1, maxcount, name);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    // should have only one data line
                    break;
                }
                Console.WriteLine("Sucessfully parsed zone: " + zoneId);
            }
            outp.Close();
        }

        static List<string> ReadPage(string url)
        {
            WebRequest wrGETURL = WebRequest.Create(url);
            Stream objStream = wrGETURL.GetResponse().GetResponseStream();
            StreamReader objReader = new StreamReader(objStream);

            string sLine = "";
            int i = 0;
            List<string> content = new List<string>();
            while (sLine != null)
            {
                i++;
                sLine = objReader.ReadLine();
                if (sLine != null)
                    content.Add(sLine);
            }
            return content;

        }
    }
}