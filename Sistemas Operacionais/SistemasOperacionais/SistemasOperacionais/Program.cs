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
            string action = Console.ReadLine();

            switch (action)
            {
                case "Search":
                    string found = Search(key);
                    if (found != null) Console.WriteLine(found);
                    else Console.WriteLine("Key does not exist");
                    break;
                case "Insert":
                    if (Insert(key, value)) Console.WriteLine("Successful insertion");
                    else Console.WriteLine("Key already Inserted");
                    break;
                case "Update":
                    if (Update(key, value)) Console.WriteLine("Successfully Updated");
                    else Console.WriteLine("Key does not exist");
                    break;
                case "Remove":
                    if (Remove(key)) Console.WriteLine("Successfully removed");
                    else Console.WriteLine("Key does not exist");
                    break;
            }
        }
        static string Search(string key)
        {

        }
        static bool Insert(string key, string v)
        {

        }
        static bool Update(string key, string v)
        {
        }
        static bool Remove(string key)
        {

        }
    }
}
