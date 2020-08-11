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
    using ProxyLib.Proxy;
    using ProxyLib.Proxy.Exceptions;

    class Program
    {
        public static void Main(string[] args) => MainAsync(args).Wait();

        static async Task MainAsync(string[] args)
        {
            var config = new ConfigurationBuilder().AddCommandLine(args).Build();
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

            if (!modules.StartsWith("HTTP/1.1 200 OK"))
            {
                throw new Exception($"Got bad response: {modules}");
            }
        }

        static async Task Iothub(string hostname, string port, string proxy)
        {
            if (proxy != null)
            {
                // Uses https://github.com/grinay/ProxyLib
                var tempProxy = proxy.Split(':');
                string proxyAddress = tempProxy[0];
                int proxyPort = int.Parse(tempProxy[1]);
                ProxyClientFactory factory = new ProxyClientFactory();
                IProxyClient proxyClient = factory.CreateProxyClient(ProxyType.Http, proxyAddress, proxyPort);

                // Setup timeouts
                proxyClient.ReceiveTimeout = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;
                proxyClient.SendTimeout = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;

                // Get TcpClient to futher work
                var client = proxyClient.CreateConnection(hostname, int.Parse(port));
                client.GetStream();
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
