using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using NDesk.Options;
using Resp;
using System.IO;
using System.Text.RegularExpressions;

namespace RespClient
{
    class RespClient
    {
        IPAddress host = IPAddress.Loopback;
        int port = 5984;

        RespClient client = new RespClient();

        public static RespClient FromArgs(string[] args)
        {
            RespClient self = new RespClient();

            OptionSet options = new OptionSet()
                .Add("host=", delegate (string value) {
                    self.host = IPAddress.Parse(value);
                })
                .Add("port=", delegate (string value) {
                    self.port = int.Parse(value);
                });

            options.Parse(args);
            return self;
        }

        public void Start()
        {
            try
            {
                this.client.Connect(this.host, this.port);

                Task[] both = new Task[2];
                both[0] = this.handleRequests();
                both[1] = this.handleResponses();
                Task.WaitAll(both);
            }
            catch (SocketException e)
            {
                Console.WriteLine("Failed to connect with: {0}", e.Message);
            }
        }

        private async Task handleRequests()
        {
            RespSerializer serializer = new RespSerializer();
            Stream input = Console.OpenStandardInput();
            byte[] bytes = new byte[1024];
            int count = 0;
            char[] sep = new char[1];
            sep[0] = ' ';

            do
            {
                Console.Write("< ");
                count = await input.ReadAsync(bytes, 0, 1024);
                if (count > 0)
                {
                    string str = Encoding.UTF8.GetString(bytes, 0, count).Trim();

                    if (str.Length == 0)
                    {
                        continue;
                    }

                    string[] pieces = str.Split(sep);
                    object[] values = new object[pieces.Length];

                    for (int i = 0; i < pieces.Length; i++) {
                        int intVal = 0;
                        if (int.TryParse(pieces[i], out intVal))
                        {
                            values[i] = intVal;
                            continue;
                        }

                        values[i] = pieces[i];
                    }

                    str = serializer.Serialize(values);
                    Console.CursorLeft = 0;
                    Console.CursorTop -= 1;
                    Console.WriteLine("< {0}", Regex.Escape(str));

                    byte[] request = Encoding.UTF8.GetBytes(str);
                    await client.GetStream().WriteAsync(request, 0, request.Length);
                }
            } while (count > 0);
        }

        private async Task handleResponses()
        {
            RespReader reader = new RespReader(this.client.GetStream());
            RespSerializer serializer = new RespSerializer();

            while (this.client.Connected)
            {
                try
                {
                    object obj = await reader.ReadAsync();
                    Console.CursorLeft = 0;
                    Console.WriteLine("> {0}", Regex.Escape(serializer.Serialize(obj)));
                    Console.Write("< ");
                }
                catch (EndOfStreamException i)
                {
                    System.Environment.Exit(0);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: {0}", e.Message);
                    System.Environment.Exit(0);
                }
            }
        }

        static void Main(string[] args)
        {
            RespClient self = RespClient.FromArgs(args);

            self.Start();
        }
    }
}
