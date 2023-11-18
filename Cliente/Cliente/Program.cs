using System;
using MSMQ.Messaging;
using System.Diagnostics;
using System.Threading;

namespace Cliente
{
    public enum Acao { Search, Update, Insert, Remove };
    class Program
    {
        const string pathFila = ".//Private$//BancoDeDadosFilaCliente";
        const string bancoFila = ".//Private$//BancoDeDadosFila";

        static void Main(string[] args)
        {
            Process processo = Process.GetCurrentProcess();
            string path = CreateQueue(processo.Id);

            while (true)
            {
                string entrada = Console.ReadLine();

                if (entrada == null) continue;

                string[] split = entrada.Split(" ", 2);
                string action = split[0];

                if (action == "quit")
                {
                    DeleteQueue(path);
                    return;
                }

                if (split.Length < 2)
                {
                    Console.WriteLine("invalid Input");
                    continue;
                }

                string[] keyAndValue = split[1].Split(",", 2);

                int key;

                if (!int.TryParse(keyAndValue[0], out key))
                {
                    Console.WriteLine("invalid command");
                    continue;
                }

                string value = null;

                if (keyAndValue.Length > 1)
                {
                    value = keyAndValue[1];
                }

                Requisicao r = null;

                switch (action)
                {
                    default:
                        Console.WriteLine("Invalid Comand");
                        break;

                    case "Search":
                        r = Search(key);
                        break;

                    case "Insert":

                        if (value == null)
                        {
                            Console.WriteLine("Invalid Input: Data value is Missing");
                            return;
                        }
                        r = Insert(key, value);
                        break;

                    case "Update":

                        if (value == null)
                        {
                            Console.WriteLine("Invalid Input: Data value is Missing");
                            return;
                        }
                        r = Update(key, value);

                        break;

                    case "Remove":
                        r = Remove(key);
                        break;
                }

                r.path = path;

                MessageQueue messageQueue = new MessageQueue(bancoFila);
                messageQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

                MessageQueue clienteFila = new MessageQueue(path);
                clienteFila.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });
                clienteFila.Purge();

                try
                {
                    messageQueue.Send(new Message(r));
                    messageQueue.Close();

                    Message cliMessage = clienteFila.Receive();
                    string resposta = (string)cliMessage.Body;
                    clienteFila.Close();

                    Console.WriteLine(resposta);

                }
                catch (MessageQueueException e)
                {
                    Console.WriteLine("error: " + e.Message);
                }
                catch (InvalidOperationException e)
                {
                    Console.WriteLine("error: " + e.Message);
                }
            }
        }

        public static Requisicao Search(int key)
        {
            Requisicao r = new Requisicao();
            r.acao = Acao.Search;
            r.key = key;
            return r;

        }
        public static Requisicao Insert(int key, string value)
        {
            Requisicao r = new Requisicao();
            r.acao = Acao.Insert;
            r.key = key;
            r.value = value;
            return r;

        }
        public static Requisicao Update(int key, string value)
        {
            Requisicao r = new Requisicao();
            r.acao = Acao.Update;
            r.key = key;
            r.value = value;
            return r;
        }
        public static Requisicao Remove(int key)
        {
            Requisicao r = new Requisicao();
            r.acao = Acao.Remove;
            r.key = key;
            return r;
        }

        static string CreateQueue(int id)
        {
            string path = pathFila + "_" + id.ToString();
            if (!MessageQueue.Exists(path))
            {
                MessageQueue.Create(path);
            }
            return path;
        }

        static void DeleteQueue(string path)
        {
            if (MessageQueue.Exists(path))
            {
                MessageQueue.Delete(path);
            }
        }
    }
    public class Requisicao
    {
        public int key;
        public string value;
        public string path;
        public Acao acao;
    }
}