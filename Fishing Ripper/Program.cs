using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

using dict = System.Collections.Generic.Dictionary<string, object>;

namespace WowHeadRipper
{
    class Program
    {

        static void Main(string[] args)
        {
            string m_readString;
            uint m_type;
            uint m_id;
            uint m_totalCount = 1;
            string m_url;
            string[] m_id_name = new string[] { "Zone", "Creature", "Gameobject", "Item" };
            string[] m_id_type = new string[] { "Fishing", "Creature", "Gameobject", "Item" };
            string[] m_id_raw_name = new string[] { "zone", "npc", "object", "item" };
            string[] m_id_DB_name = new string[] { "Fishing_loot_template", "Creature_loot_template", "Gameobject_loot_template", "Item_loot_template"};
            Console.WriteLine("Welcome to SkyFire data parser");
            StreamWriter file = File.CreateText("loot.sql");
        menu:
            Console.WriteLine("Please select parser type:");
            Console.WriteLine("0 - Fishing loot parser");
            Console.WriteLine("1 - Creature loot parser");
            Console.WriteLine("2 - Gameobject loot parser");
            Console.WriteLine("3 - Item loot parser");
            Console.WriteLine("4 - Exit application");

            m_readString = Console.ReadLine();

            if (!uint.TryParse(m_readString, out m_type))
            {
                Console.WriteLine("Incorrect Value!");
                goto menu;
            }

            if (m_type < 0 || m_type > 4)
            {
                Console.WriteLine("Incorrect Value!");
                goto menu;
            }

            if (m_type == 4)
            {
                file.Close();
                Environment.Exit(0);
            }

            Console.WriteLine("Enter {0} Id:", m_id_name[m_type]);
            m_readString = Console.ReadLine();

            if (!uint.TryParse(m_readString, out m_id))
            {
                Console.WriteLine("Incorrect Value!");
                goto menu;
            }

            m_url = "http://www.wowhead.com/" + m_id_raw_name[m_type] + "=" + m_id;

            List<string> content;
            try
            {
                content = ReadPage(m_url);
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Id {1} Doesn't exist ({2})", m_id_name[m_type], m_id, e.Message);
                goto menu;
            }

            Regex r = new Regex(@"new Listview\(\{template: 'item', id: 'fishing'.*data: (\[.+\])\}\);");
            Regex r2 = new Regex(@"new Listview\(\{template: 'item', id: 'fishing'.*_totalCount:");
            if (m_type == 1)
            {
                r = new Regex(@"new Listview\(\{template: 'item', id: 'drops'.*data: (\[.+\])\}\);");
                r2 = new Regex(@"new Listview\(\{template: 'item', id: 'drops'.*computeDataFunc:");
            }
            if (m_type == 2)
            {
                r = new Regex(@"new Listview\(\{template: 'item', id: 'mining'.*data: (\[.+\])\}\);");
                r2 = new Regex(@"new Listview\(\{template: 'item', id: 'mining'.*_totalCount:");
            }
            if (m_type == 3)
            {
                r = new Regex(@"new Listview\(\{template: 'item', id: 'contains'.*data: (\[.+\])\}\);");
                r2 = new Regex(@"new Listview\(\{template: 'item', id: 'contains'.*_totalCount:");
            }
            foreach (string line in content)
            {
                Match m2 = r2.Match(line);
                Match m = r.Match(line);
                if (m2.Success)
                {
                    string str = m2.Groups[0].Captures[0].Value;
                    string[] numbers = Regex.Split(str, @"\D+");
                    if (m_type != 1 || m_type != 3)
                        m_totalCount = uint.Parse(numbers[2]);
                    else
                        m_totalCount = uint.Parse(numbers[1]);
                }
                if (!m.Success)
                {
                    continue;
                }
                file.WriteLine("-- Parsed {0} loot for {1} id : {2} ", m_id_type[m_type], m_id_name[m_type], m_id);
                file.WriteLine("DELETE FROM `{0}` WHERE `Entry` = {1}", m_id_DB_name[m_type], m_id);
                file.WriteLine();
                var json = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
                string data = m.Groups[1].Captures[0].Value;
                data = data.Replace("[,", "[0,");   // otherwise deserializer complains
                object[] m_object = (object[])json.DeserializeObject(data);
                foreach (dict objectInto in m_object)
                {
                    try
                    {
                        int id = (int)objectInto["id"];
                        int maxcount = 1;
                        int mincount = 1;
                        float pct = 0.0f;
                        string name = "";
                        int lootmode = 0;
                        if (m_type == 0)
                            lootmode = 1;

                        if (objectInto.ContainsKey("name"))
                            name = (string)objectInto["name"];
                        int count = (int)objectInto["count"];
                        int ArraySize = ((Array)objectInto["stack"]).GetLength(0);
                        int[] stack = new int[ArraySize];
                        Array.Copy((Array)objectInto["stack"], stack, ArraySize);
                        pct = (float)count / m_totalCount * 100.0f;
                        maxcount = stack[1];
                        mincount = stack[0];
                        Math.Round(pct, 3);
                        string strpct = pct.ToString();
                        strpct = strpct.Replace(",", ".");
                        file.WriteLine("INSERT INTO `{0}` VALUES ( '{1}', '{2}', '{3}', '{4}', '{5}', '{6}' , '{7}'); -- {8}",
                        m_id_DB_name[m_type], m_id, id, strpct, 1, lootmode, mincount, maxcount, name);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    // should have only one data line
                    break;
                }
            Console.WriteLine();
            Console.WriteLine("Sucessfully parsed {0}: {1}", m_id_name[m_type], m_id);
            Console.WriteLine();
            file.WriteLine();
            file.Flush();
            goto menu;
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