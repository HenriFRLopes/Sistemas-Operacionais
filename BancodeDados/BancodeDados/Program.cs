﻿using System;
using System.IO;
using MSMQ.Messaging;

namespace SistemasOperacionais
{
    class Program
    {
        //variaveis do arquivo de banco de dados
        const string path = "bancoDeDados.db";
        const string temporaryFile = "Temporary_";
        //Variaveis da fila de processos
        const string pathFila = ".//Private$//BancoDeDadosFila";
        const string clienteFila = ".//Private$//BancoDeDadosFilaCliente";
        static void Main(string[] args)
        {

            //pega as linhas de argumento do usuario
            if (args.Length == 0) return;
         
            string[] split = args[0].Split('=');
            if(split.Length < 2)
            {
                Console.WriteLine("Invalid Input: Missing '='");
                return;

            }

            //O usuario pode usar os comandos: Search, Insert, Update e Remove
            string action = split[0];

            //Separa o argumento na variavel keyAndValue 
            string[] keyAndValue = split[1].Split(':', 2); // [0] = Chave do Dado (sempre uma int) [1] = Valor do Dado

            if(keyAndValue[0] == "")
            {
                Console.WriteLine("Invalid Input: Missing Key")
            }
            
            int key;

            //Verifica se a chave é um numero inteiro
            if(!int.TryParse(keyAndValue[0], out key))
            {
                Console.WriteLine("Invalid Input: Key must be an integer number");
                return;
            }

            //Verifica se a cahve é um numero positivo
            if(keyAndValue[0] < 0)
            {
                Console.WriteLine("Invalid Input: Key must be a positive number");
                return;
            }


            //cria uma variavel do tipo Data
            Data d = new Data(keyAndValue[0], keyAndValue[1]);

            try
            {

                switch (action)
                {
                    default:
                        Console.WriteLine("Invalid Comand");
                        break;

                    case "Search":

                        if (keyAndValue.Length == 1)
                        {
                            string found = Search(d);

                            if (found != null) Console.WriteLine(found);

                            else Console.WriteLine("Key does not exist");
                        }

                        else
                        {
                            Console.WriteLine("Invalid Input: More values than needed");
                            return;
                        }

                        break;

                    case "Insert":

                        if(keyAndValue.Length <2)
                        {
                            Console.WriteLine("Invalid Input: Data value is Missing");
                            return;
                        }

                        else
                        {
                            if(Insert(d)) Console.WriteLine(keyAndValue[0]);

                            else Console.WriteLine("Key is already inserted");
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
                            if (Update(d)) Console.WriteLine("Successfully Updated");

                            else Console.WriteLine("Key does not exist");

                        }

                        break;

                    case "Remove":

                        if (keyAndValue.Length == 1)
                        {
                            if (Remove(d)) Console.WriteLine("Successfully removed");

                            else Console.WriteLine("Key does not exist");
                        }

                        else
                        {
                            Console.WriteLine("Invalid Input: More values than needed");
                            return;
                        }

                        break;
                }
            }
            catch (Exception exeception)
            {
                Console.WriteLine(exeception.Message);
            }
        }
        static string Search(Data d)
        {
            if (!File.Exists(path)) return null;
            StreamReader file = new StreamReader(path);

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
        // Utiliza o StreamWriter para receber ou criar um arquivo novo, independente do caso, este metodo abre o arquivo, lê e insere as informãções (caso este dado não exista ainda) e fecha o arquivo
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

            Console.WriteLine("Successful insertion");

            return true;
        }
        //utiliza um arquivo temporario para armazenar as informações antigas do dado e substitui as informações que precisam ser atualizadas
        static bool Update(Data d)
        {
            string path2 = temporaryFile + path;
            bool updated = false;
            StreamWriter tempFile = new StreamWriter(path2, true);
            StreamReader file = new StreamReader(path, true);
            if (file == null || file.EndOfStream || tempFile == null)
            {
                return false;
            }
            string text = file.ReadLine();
            while (text != null)
            {
                string[] split = text.Split(':');
                if (!updated && split[0] == d.key)
                {
                    tempFile.WriteLine(d.key + ":" + d.value);
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
            return updated;
        }
        //utiliza um arquivo temporario que tem as informações antigas e compara o "key" do dado inserido com as informações do arquivo original para deletar
        static bool Remove(Data d)
        {
            string path2 = temporaryFile + path;
            bool removed = false;
            StreamWriter tempFile = new StreamWriter(path2, true);
            StreamReader file = new StreamReader(path);
            if (file == null || file.EndOfStream || tempFile == null)
            {
                return false;
            }
            string text = file.ReadLine();
            while (text != null)
            {
                string[] split = text.Split(':');
                if (split[0] == d.key) removed = true;
                else tempFile.WriteLine(text);
                text = file.ReadLine();
            }
            file.Close();
            tempFile.Close();
            if (removed)
            {
                //se encontrar o dado inserido substitui o arquivo pelo temporario, que não possui dados removidos
                File.Delete(path);
                File.Move(temporaryFile, path);
            }
            else
            {
                //se não emcontrar "key" do dado inserido, deleta o arquivo temporario
                File.Delete(temporaryFile);
            }
            return removed;
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