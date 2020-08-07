// Copyright (c) Microsoft. All rights reserved.
namespace Diagnostics
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    class Program
    {
        public static void Main(string[] args) => MainAsync(args).Wait();

        static async Task MainAsync(string[] args)
        {
            Console.WriteLine(JsonConvert.SerializeObject(args));

            var config = new ConfigurationBuilder().AddCommandLine(args).Build();
            Console.WriteLine(config["arg1"]);

            switch (args[0])
            {
                case "edge-agent":
                    await EdgeAgent(config["management-uri"]);
                    break;
                case "iothub":
                    await Iothub(config["hostname"], config["port"], config["proxy"]);
                    break;
                case "local-time":
                    Console.WriteLine(DateTime.Now.ToUnixTimestamp());
                    break;
                default:
                    throw new Exception("Invalid args");
            }
        }
        static async Task EdgeAgent(string managementUri)
        {
            string modules;
            if (managementUri.EndsWith(".sock"))
            {
                modules = GetSocket.GetSocketResponse(managementUri, "/modules/?api-version=2018-06-28");
            }
            else
            {
                using (var http = new HttpClient())
                using (var response = await http.GetAsync(managementUri + "/modules/?api-version=2018-06-28"))
                {
                    response.EnsureSuccessStatusCode();
                    modules = await response.Content.ReadAsStringAsync();
                }
            }

            if (modules.Length == 0)
            {
                throw new Exception("no module response");
            }
        }

        static async Task Iothub(string hostname, string port, string proxy)
        {
            if (proxy != null)
            {
                TcpClient client = new TcpClient();
                await client.ConnectAsync(hostname, int.Parse(port));
                var stream = client.GetStream();

                // Based on https://stackoverflow.com/questions/3127127/how-to-open-socket-thru-proxy-server-in-net-c/3127176#3127176
                await stream.WriteAsync(Encoding.UTF8.GetBytes("CONNECT Host:Port HTTP/1.1<CR><LF>"));
                await stream.WriteAsync(Encoding.UTF8.GetBytes("<CR><LF>"));
                byte[] buffer = new byte[4096];
                int len = await stream.ReadAsync(buffer);
                string result = Encoding.UTF8.GetString(buffer, 0, len);
                if (!result.Contains("200"))
                {
                    throw new Exception("could not connect through proxy");
                }
            }
            else
            {
                TcpClient client = new TcpClient();
                await client.ConnectAsync(hostname, int.Parse(port));
                client.GetStream();
            }
        }
    }

}
