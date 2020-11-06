// Copyright (c) Microsoft. All rights reserved.
namespace Diagnostics
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                MainAsync(args).Wait();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            return 0;
        }

        static async Task MainAsync(string[] args)
        {
            var config = new ConfigurationBuilder().AddCommandLine(args).Build();
            switch (args[0])
            {
                case "parent-hostname":
                    ParentHostname(config["parent-hostname"]);
                    break;
                case "edge-agent":
                    await EdgeAgent(config["management-uri"]);
                    break;
                case "upstream":
                    await Upstream(config["hostname"], config["port"], config["proxy"]);
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

        static async Task Upstream(string hostname, string port, string proxy)
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
            {
                    return true; // Is valid
            };

            if (proxy != null)
            {
                Environment.SetEnvironmentVariable("https_proxy", proxy);
            }

            var httpClient = new HttpClient(httpClientHandler);
            var logsUrl = string.Format("https://{0}/devices/0000/modules", hostname);
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, logsUrl);
            HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

            var keys = httpResponseMessage.Headers.GetValues("iothub-errorcode");
            if (!keys.Contains("InvalidProtocolVersion"))
            {
                throw new Exception($"Wrong value for iothub-errorcode header");
            }
        }

        static void ParentHostname(string parent_hostname)
        {
            _ = Dns.GetHostEntry(parent_hostname);
        }
    }
}
