using System;

namespace SistemasOperacionais
{
    class Program
    {
        string path = "bancoDeDados.db";
        static void Main(string[] args)
        {
            string key = Console.ReadLine();
            string value = Console.ReadLine();
            Data d = new Data(key, value);
            string action = Console.ReadLine();

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
        }
        static bool Insert(Data d)
        {

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
