using System;
using System.IO;

namespace SistemasOperacionais
{
    class Program
    {
        string path = "bancoDeDados.db";
        static void Main(string[] args)
        {
            if (args.Length == 0) return;
            string[] split = args[0].Split('=');
            string[] keyAndValue = split[1].Split(':');
            Data d = new Data(keyAndValue[0], keyAndValue[1]);
            string action = split[0];

            switch (action)
            {
                case "Search":
                    string found = Search(d);
                    if (found != null) Console.WriteLine(found);
                    else Console.WriteLine("Key does not exist");
                    break;
                case "Insert":
                    if (Insert(d)) Console.WriteLine("Successful insertion");
                    else Console.WriteLine("Key already Inserted");
                    break;
                case "Update":
                    if (Update(d)) Console.WriteLine("Successfully Updated");
                    else Console.WriteLine("Key does not exist");
                    break;
                case "Remove":
                    if (Remove(d) Console.WriteLine("Successfully removed");
                    else Console.WriteLine("Key does not exist");
                    break;
            }
        }
        static string Search(Data d)
        {
            StreamReader file = new StreamReader(path, true);

            if (file.EndOfStream)
            {
                return null;
            }

            string text = file.ReadLine();

            while (text != null)
            {
                string[] splitData = text.Split(':');

                if (splitData[0] == d.key)
                {
                    return splitData[1];
                }

                text = file.ReadLine();
            }

            file.Close();

            return null;
        }
        static bool Insert(Data d)
        {
            if (Search(d) != null)
            {
                return false;
            }

            StreamWriter file = new StreamWriter(path, true);

            file.BaseStream.Seek(0, SeekOrigin.End);
            file.WriteLine(d.key + ":" + d.value);

            file.Close();

            return true;
        }
        static bool Update(Data d)
        {
        }
        static bool Remove(Data d)
        {

        }
    }
    class Data
    {
        public string key, value;
        public Data(string k, string v)
        {
            this.key = k;
            this.value = v;
        }
    }

}
