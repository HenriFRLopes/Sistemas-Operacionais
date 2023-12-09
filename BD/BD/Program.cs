using System;
using System.IO;
using System.Threading;
using MSMQ.Messaging;

namespace BD
{
    public enum Acao { Search, Update, Insert, Remove };
    class Program
    {
        //variaveis do arquivo de banco de dados
        const string path = "bancoDeDados.db";
        const string temporaryFile = "Temporary_";

        //Variaveis da fila de processos
        const string pathFila = ".\\Private$\\BancoDeDadosFila";
        const string clienteFila = ".\\Private$\\BancoDeDadosFilaCliente";

        static void Main(string[] args)
        {
            Controller controller = new Controller(path);

            //pega as linhas de argumento do usuario
            if (args.Length > 0)
            {
                AcharAcao(args, controller);
                return;
            }

            CreateQueue();

            MessageQueue mq = new MessageQueue(pathFila);
            mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(Requisicao) });
            mq.Purge();
            //Deletar a fila 
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                DeleteQueue();
            };

            while (true)
            {
                try
                {
                    Message message = mq.Receive();
                    Requisicao r = (Requisicao)message.Body;

                    //Thread de Resposta do Cliente
                    Thread thread = new Thread(() => Resposta(controller, r));
                    thread.Start();
                }
                catch (MessageQueueException e)
                {
                    Console.WriteLine("Invalid Action: " + e.Message);
                }
            }

        }
        static void Resposta(Controller c, Requisicao r)
        {
            //Processa a resposta
            string answer = c.Action(r);

            //Envia a resposta
            MessageQueue clienteFila = new MessageQueue(r.path);
            clienteFila.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });
            clienteFila.Send(new Message(answer));
            clienteFila.Close();
        }

        static void AcharAcao(string[] args, Controller c)
        {
            // Argumentos do programa, para pegar a ação que o usuario quer utlizar
            string[] split = args[0].Split('=');
            if (split.Length < 2)
            {
                Console.WriteLine("Invalid Input: Missing '='");
                return;

            }

            //O usuario pode usar os comandos: Search, Insert, Update e Remove
            string action = split[0];

            //Separa o argumento na variavel keyAndValue
            string[] keyAndValue = split[1].Split(':', 2); // [0] = Chave do Dado (sempre uma int) [1] = Valor do Dado

            if (keyAndValue[0] == "")
            {
                Console.WriteLine("Invalid Input: Missing Key");
            }

            int key;

            //Verifica se a chave é um numero inteiro
            if (!int.TryParse(keyAndValue[0], out key))
            {
                Console.WriteLine("Invalid Input: Key must be an integer number");
                return;
            }

            //Verifica se a cahve é um numero positivo
            if (key < 0)
            {
                Console.WriteLine("Invalid Input: Key must be a positive number");
                return;
            }

            Requisicao r = new Requisicao();
            r.key = key;

            switch (action)
            {
                default:
                    Console.WriteLine("Invalid Comand");
                    break;

                case "Search":

                    if (keyAndValue.Length == 1) r.acao = Acao.Search;
                    else return;
                    break;

                case "Insert":

                    if (keyAndValue.Length < 2)
                    {
                        Console.WriteLine("Invalid Input: Data value is Missing");
                        return;
                    }
                    else
                    {
                        r.value = keyAndValue[1];
                        r.acao = Acao.Insert;
                    }
                    break;

                case "Update":

                    if (keyAndValue.Length < 2)
                    {
                        Console.WriteLine("Invalid Input: Data value is Missing");
                        return;
                    }
                    else
                    {
                        r.value = keyAndValue[1];
                        r.acao = Acao.Update;
                    }

                    break;

                case "Remove":

                    if (keyAndValue.Length == 1)
                    {
                        r.acao = Acao.Remove;
                    }
                    else
                    {
                        Console.WriteLine("Invalid Input: More values than needed");
                        return;
                    }
                    break;
            }

            try
            {
                string answer = c.Action(r);
                Console.WriteLine(answer);
            }
            catch (Exception e)
            {
                Console.WriteLine("Inavlid Action: " + e.Message);
            }
        }

        static void CreateQueue()
        {
            if (!MessageQueue.Exists(pathFila))
            {
                MessageQueue.Create(pathFila);
            }
        }

        static void DeleteQueue()
        {
            if (MessageQueue.Exists(pathFila))
            {
                MessageQueue.Delete(pathFila);
                Console.WriteLine("Queue deleted");
            }
            else
            {
                Console.WriteLine("Queue inexistent");
            }
        }

    }
    public class Controller: Comandos
    {
        string path;
        string temporaryFile;

        Mutex mutex;
        Semaphore semaforo;
        int leitores = 0;

        public Controller(string path, string temporaryFile = "Temporary_")
        {
            this.path = path;
            this.temporaryFile = temporaryFile;
            mutex = new Mutex();
            semaforo = new Semaphore(1, 1);
            leitores = 0;
        }

        public override string Search(int key)
        {
            string found = null;
            bool encontrado = false;

            if (!File.Exists(path)) return null;

            //Mutex Lock
            mutex.WaitOne();
            leitores++;

            if (leitores == 1)
            {
                //Semaforo Dow
                semaforo.WaitOne();
            }

            //Mutex Unlock
            mutex.ReleaseMutex();

            StreamReader file = new StreamReader(path);

            if (file.EndOfStream)
            {
                semaforo.Release();
                return null;
            }

            string text = file.ReadLine();

            while (text != null)
            {
                string[] splitData = text.Split(',');

                if (splitData[0] == key.ToString())
                {
                    found = splitData[1];
                    encontrado = true;
                    break;
                }

                text = file.ReadLine();
            }

            file.Close();

            //Mutex Lock
            mutex.WaitOne();
            leitores--;

            if (leitores == 0)
            {
                //Semaforo Up
                semaforo.Release();
            }

            //Mutex Unlock
            mutex.ReleaseMutex();

            if (encontrado) return found;

            else return null;
        }

        // Utiliza o StreamWriter para receber ou criar um arquivo novo, independente do caso, este metodo abre o arquivo, lê e insere as informãções (caso este dado não exista ainda) e fecha o arquivo
        public override bool Insert(int key, string value)
        {
            if (Search(key) != null)
            {
                return false;
            }

            //Semaforo Down
            semaforo.WaitOne();

            StreamWriter file = new StreamWriter(path, true);

            file.BaseStream.Seek(0, SeekOrigin.End);
            file.WriteLine(key + "," + value);
            file.Close();

            //Semaforo Up
            semaforo.Release();

            Console.WriteLine("Successful insertion");

            return true;
        }

        //utiliza um arquivo temporario para armazenar as informações antigas do dado e substitui as informações que precisam ser atualizadas
        public override bool Update(int key, string value)
        {
            string path2 = temporaryFile + path;
            bool updated = false;

            //Semaforo Down
            semaforo.WaitOne();


            StreamWriter tempFile = new StreamWriter(path2, true);
            StreamReader file = new StreamReader(path, true);

            if (file == null || file.EndOfStream || tempFile == null)
            {
                //Semaforo Up
                semaforo.Release();
                return false;
            }

            string text = file.ReadLine();

            while (text != null)
            {
                string[] split = text.Split(',');

                if (!updated && split[0] == key.ToString())
                {
                    tempFile.WriteLine(key + "," + value);
                    updated = true;
                }

                else tempFile.WriteLine(text);

                text = file.ReadLine();
            }

            file.Close();
            tempFile.Close();

            if (updated)
            {
                //substitui as informações antigas pelas novas
                File.Delete(path);
                File.Move(path2, path);
            }

            else
            {
                //se não emcontrar "key" do dado inserido, deleta o arquivo temporario
                File.Delete(path2);

            }

            //Semaforo Up
            semaforo.Release();

            return updated;
        }

        //utiliza um arquivo temporario que tem as informações antigas e compara o "key" do dado inserido com as informações do arquivo original para deletar
        public override bool Remove(int key)
        {
            if (!File.Exists(path)) return false;

            string path2 = temporaryFile + path;
            bool removed = false;

            //Semaforo Down
            semaforo.WaitOne();

            StreamWriter tempFile = new StreamWriter(path2, true);
            StreamReader file = new StreamReader(path);

            if (file == null || file.EndOfStream || tempFile == null)
            {
                //Semaforo Up
                semaforo.Release();
                return false;
            }

            string text = file.ReadLine();

            while (text != null)
            {
                string[] split = text.Split(',');

                if (split[0] == key.ToString()) removed = true;

                else tempFile.WriteLine(text);

                text = file.ReadLine();
            }

            file.Close();
            tempFile.Close();

            if (removed)
            {
                //se encontrar o dado inserido substitui o arquivo pelo temporario, que não possui dados removidos
                File.Delete(path);
                File.Move(path2, path);
            }

            else
            {
                //se não emcontrar "key" do dado inserido, deleta o arquivo temporario
                File.Delete(path2);
            }

            //Semaforo Up
            semaforo.Release();

            return removed;
        }

        public override void Fechar()
        {
        }

    }
    public class Requisicao
    {
        public int key;
        public string value;
        public string path;
        public Acao acao;
    }

    public abstract class Comandos
    {
        public string Action(Requisicao r)
        {
            switch (r.acao)
            {
                default:
                    return "Invalid Comand";

                case Acao.Search:

                    string found = Search(r.key);
                    if (found != null) return found;
                    else return "Key does not exist";

                case Acao.Insert:

                    if (Insert(r.key, r.value)) return r.value.ToString();
                    else return "Key is already inserted";

                case Acao.Update:

                    if (Update(r.key, r.value)) return "Successfully Updated";
                    else return "Key does not exist";

                case Acao.Remove:

                    if (Remove(r.key)) return "Successfully removed";

                    else return "Key does not exist";
            }
        }

        public abstract string Search(int key);
        public abstract bool Insert(int key, string value);
        public abstract bool Update(int key, string value);
        public abstract bool Remove(int key);
        public abstract void Fechar();
    }

}