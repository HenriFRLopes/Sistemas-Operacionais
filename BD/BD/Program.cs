using System;
using System.IO;
using System.Threading;
using MSMQ.Messaging;
using System.Collections.Generic;

namespace BD
{
    public enum Acao { Search, Update, Insert, Remove };
    class Program
    {
        //variaveis do arquivo de banco de dados
        const string path = "BD.db";
        const string temporaryFile = "Temporary_";

        const int updateTimer = 2000;

        static bool ativo = true;

        //Variaveis da fila de processos
        const string pathFila = ".\\Private$\\BancoDeDadosFila";
        const string clienteFila = ".\\Private$\\BancoDeDadosFilaCliente";

        static void Main(string[] args)
        {
            Comandos controller = new Controller(path);

            //pega as linhas de argumento do usuario
            if (args.Length > 0)
            {
                if(AcharAcao(args, ref controller))
                {
                    return;
                }
            }

            CreateQueue();

            MessageQueue mq = new MessageQueue(pathFila);
            mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(Requisicao) });
            mq.Purge();
            //Deletar a fila 
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                controller.Close();
                DeleteQueue();
                ativo = false;
            };

            if (controller is LRU || controller is CacheDoAging)
            {
                Thread thread = new Thread(() => ChangeCache(controller));
                thread.Start();
            }

            while (ativo)
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

            static void ChangeCache(Comandos controller)
            {
                while(ativo)
                {
                    Thread.Sleep(updateTimer);
                    controller.Change();
                }
            }

        }
        static void Resposta(Comandos c, Requisicao r)
        {
            if(r.path == null)
            {
                return;
            }

            if (!MessageQueue.Exists(r.path))
            {
                return;
            }



            //Processa a resposta
            string answer = c.Action(r);

            //Envia a resposta
            MessageQueue clienteFila = new MessageQueue(r.path);
            clienteFila.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });
            clienteFila.Send(new Message(answer));
            clienteFila.Close();
        }

        static bool AcharAcao(string[] args, ref Comandos c)
        {
            // Argumentos do programa, para pegar a ação que o usuario quer utlizar
            string[] split = args[0].Split('=');
            if (split.Length < 2)
            {
                Console.WriteLine("Invalid Input: Missing '-'");
                return true;
            }

            //O usuario pode usar os comandos: Search, Insert, Update e Remove
            string action = split[0];

            //Separa o argumento na variavel keyAndValue
            string[] keyAndValue = split[1].Split('-', 2); // [0] = Chave do Dado (sempre uma int) [1] = Valor do Dado

            if (keyAndValue[0] == "")
            {
                Console.WriteLine("Invalid Input: Missing Key");
                return true;
            }

            int key;

            //Verifica se a chave é um numero inteiro
            if (!int.TryParse(keyAndValue[0], out key))
            {
                Console.WriteLine("Invalid Input: Key must be an integer number");
                return true;
            }

            //Verifica se a cahve é um numero positivo
            if (key < 0)
            {
                Console.WriteLine("Invalid Input: Key must be a positive number");
                return true;
            }

            Requisicao r = new Requisicao();
            r.key = key;

            switch (action)
            {
                default:
                    Console.WriteLine("Invalid Comand");
                    break;

                case "Search":

                    if (keyAndValue.Length == 1)
                    {
                        r.acao = Acao.Search;
                    }
                    else
                    {
                        return true;
                    }
                    break;

                case "Insert":

                    if (keyAndValue.Length < 2)
                    {
                        Console.WriteLine("Invalid Input: Data value is Missing");
                        return true;
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
                        return true;
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
                        return true;
                    }
                    break;
                case "CacheSize":
                    if (keyAndValue.Length < 2)
                    {
                        Console.WriteLine("Invalid Input: Data value is Missing");
                        return true;
                    }
                    Comandos banco = CacheBanco.GetCache(c, key, keyAndValue[1]);
                    if (banco == null)
                    {
                        Console.WriteLine("invalid command: invalid cache values");
                        return true;
                    }
                     c = banco;
                    return false;
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
            return true;
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

        public override void Change(){}
        public override void Close(){}

    }

    public class Requisicao
    {
        public int key;
        public string value;
        public string path;
        public Acao acao;
        public bool bitR = false;
        public bool bitM = false;
        public bool recente = false;
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
        public abstract void Change();
        public abstract void Close();
    }

    public class CacheBanco : Comandos
    {
        public List<Requisicao> cache;
        public int size;

        public Comandos bancoDados;

        protected Semaphore semaforo;
        protected Mutex mutex;
        protected int readers;

        public CacheBanco(Comandos dataBank, int size)
        {
            this.bancoDados = dataBank;
            this.size = size;
            cache = new List<Requisicao>(size);

            semaforo = new Semaphore(1, 1);
            mutex = new Mutex();
            readers = 0;
        }

        protected virtual void PrintCache()
        {
            Console.Write("[");
            foreach (Requisicao requisicao in cache)
            {
                Console.Write(requisicao.key + " ");
            }
            Console.WriteLine("]");
        }

        Requisicao GetRequisicaoCache(int key)
        {
            foreach (Requisicao requisicao in cache)
            {
                if (requisicao.key == key)
                {
                    requisicao.bitR = true;
                    return requisicao;
                }
            }

            return null;
        }

        Requisicao GetRequisicaoDataBase(int key)
        {
            string value = bancoDados.Search(key);

            if (value == null)
            {
                return null;
            }
            Requisicao requisicao = CreateRequisicao(key, value);
            return requisicao;
        }

        protected Requisicao GetRequisicao(int key)
        {
            DownReaders();

            Requisicao requisicao = GetRequisicaoCache(key);

            UpReaders();

            if (requisicao != null)
            {
                return requisicao;
            }

            requisicao = GetRequisicaoDataBase(key);

            if (requisicao != null)
            {
                InsertInCache(requisicao);
                return requisicao;
            }

            return null;
        }

        protected void ExecutarRequisicao(Requisicao requisicao)
        {
            if (requisicao.value == null) bancoDados.Remove(requisicao.key);
            else if (requisicao.recente) bancoDados.Insert(requisicao.key, requisicao.value);
            else if (requisicao.bitM) bancoDados.Update(requisicao.key, requisicao.value);
        }

        protected bool InsertInCache(Requisicao requisicao)
        {

            if (cache.Count >= size)
            {
                SubstituirPagina(requisicao);
            }
            else
            {
                semaforo.WaitOne();
                cache.Add(requisicao);
                semaforo.Release();
            }

            return true;
        }

        protected virtual Requisicao SubstituirRequisicao()
        {
            return cache[0];
        }
        protected virtual void SubstituirPagina(Requisicao requisicao)
        {
            semaforo.WaitOne();
            Requisicao exit = cache[0];
            cache.Remove(exit);
            cache.Add(requisicao);
            semaforo.Release();

            Thread thread = new Thread(() => ExecutarRequisicao(exit));
            thread.Start();
        }

        void RemoveFromCache(Requisicao requisicao)
        {
            cache.Remove(requisicao);
        }

        protected virtual Requisicao CreateRequisicao(int key, string value)
        {
            Requisicao requisicao = new Requisicao();
            requisicao.key = key;
            requisicao.value = value;
            requisicao.bitR = true;
            return requisicao;
        }

        public override string Search(int key)
        {
            Requisicao requisicao = GetRequisicao(key);
            if (requisicao == null) return null;
            semaforo.WaitOne();
            requisicao.bitR = true;
            semaforo.Release();

            return requisicao.value;
        }

        public override bool Insert(int key, string value)
        {
            Requisicao requisicao = GetRequisicao(key);

            if (requisicao == null)
            {
                requisicao = CreateRequisicao(key, value);
                requisicao.recente = true;
                requisicao.bitM = true;
                InsertInCache(requisicao);
            }
            else if (requisicao.value == null)
            {
                semaforo.WaitOne();
                requisicao.value = value;
                requisicao.bitM = true;
                requisicao.bitR = true;
                semaforo.Release();
            }
            else return false;

            return true;
        }

        public override bool Update(int key, string nvalue)
        {
            Requisicao requisicao = GetRequisicao(key);
            if (requisicao == null || requisicao.value == null) return false;

            semaforo.WaitOne();
            requisicao.value = nvalue;
            requisicao.bitM = true;
            requisicao.bitR = true;
            semaforo.Release();


            return true;
        }

        public override bool Remove(int key)
        {
            Requisicao requisicao = GetRequisicao(key);
            if (requisicao == null) return false;
            semaforo.WaitOne();
            if (requisicao.recente)
            {
                cache.Remove(requisicao);
                semaforo.Release();
                return true;
            }

            requisicao.value = null;
            requisicao.bitM = true;
            requisicao.bitR = true;
            semaforo.Release();
            return true;
        }

        public override void Change()
        {
            bancoDados.Change();
        }

        public override void Close()
        {
            foreach (Requisicao requisicao in cache)
            {
                ExecutarRequisicao(requisicao);
            }

            bancoDados.Close();
        }

        protected void UpReaders()
        {
            mutex.WaitOne();
            readers--;
            if (readers == 0)
            {
                semaforo.Release();
            }
            mutex.ReleaseMutex();
        }

        protected void DownReaders()
        {
            mutex.WaitOne();
            readers++;
            if(readers == 1)
            {
                semaforo.WaitOne();
            }
            mutex.ReleaseMutex();
        }
        public static CacheBanco GetCache(Comandos bancoDados, int size, string opcao)
        {
            if (size <= 0)
            {
                return null;
            }

            switch (opcao)
            {
                case "FIFO":
                    return new CacheBanco(bancoDados, size);
                case "LRU":
                    return new LRU(bancoDados, size);
                case "Aging":
                    return new CacheDoAging(bancoDados, size);
                default:
                    return null;
            }
        }
    }

    public class LRU : CacheBanco
    {
        public LRU(Comandos bancoDados, int size) : base(bancoDados, size) { }

        protected override void PrintCache()
        {
            Console.Write("[");
            foreach (Requisicao requisicao in cache)
            {
                Console.Write("{" + requisicao.key + ",R:" + (requisicao.bitR ? '1' : '0') + ",M:" + (requisicao.bitM ? '1' : '0') + "} ");
            }
            Console.WriteLine("]");
        }

        protected override Requisicao SubstituirRequisicao()
        {
            Requisicao? candidate = cache[0];
            int maxPriority = (candidate.bitR ? 0: 2) + (candidate.bitM ? 0: 1);

            for (int i = 0; i < cache.Count; i++)
            {
                Requisicao rqs = cache[i];
                int priority = (rqs.bitR ? 0 : 2) + (rqs.bitM ? 0 : 1);

                if (priority > maxPriority)
                {
                    candidate = rqs;
                    maxPriority = priority;
                }
            }
            return candidate;
        }

        public override void Change()
        {
            foreach (Requisicao requisicao in cache)
            {
                requisicao.bitR = false;
            }
        }
    }
    public class AgingDaRequisicao: Requisicao
    {
        public int count = 128; 
    }

    public class CacheDoAging : CacheBanco
    {
        public CacheDoAging(Comandos bancoDados, int size) : base(bancoDados, size)
        {
        }

        protected override void PrintCache()
        {
            Console.Write("[");
            foreach (AgingDaRequisicao requisicao in cache)
            {
                Console.Write("{" + requisicao.key + ",R:" + (requisicao.bitR ? '1' : '0') + ",Time:" + requisicao.count + "} ");
            }
            Console.WriteLine("]");
        }

        protected override Requisicao CreateRequisicao(int key, string value)
        {
            AgingDaRequisicao requisicao = new AgingDaRequisicao();
            requisicao.key = key;
            requisicao.value = value;
            requisicao.bitR = true;
            return requisicao;
        }

        protected override Requisicao SubstituirRequisicao()
        {
            AgingDaRequisicao? candidate = (AgingDaRequisicao) cache[0];
            int minCount = candidate.count;

            for (int i = 0; i < cache.Count; i++)
            {
                AgingDaRequisicao rqs = (AgingDaRequisicao)cache[i];

                if (rqs.count < minCount || candidate == null)
                {
                    candidate = rqs;
                    minCount = rqs.count;
                }
            }
            return candidate;
        }

        public override void Change()
        {
            foreach (Requisicao requisicao in cache)
            {
                AgingDaRequisicao rqs = (AgingDaRequisicao)requisicao;
                rqs.count /= 2;

                if (rqs.bitR)
                {
                    rqs.count += 128;
                    rqs.bitR = false;
                }
            }
        }
    }


}