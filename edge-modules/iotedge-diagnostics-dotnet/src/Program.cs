// Copyright (c) Microsoft. All rights reserved.
namespace Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using ProxyLib.Proxy;

    class Program
    {
        const string CLIENT_WORKLOAD_API_VERSION = "2019-01-30";
        const int SOCKET_STREAM_WAIT_INTERVAL_MS = 50;
        const int SOCKET_STREAM_WAIT_TIMEOUT_MS = 15000;

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
                    await Upstream(config["hostname"], config["port"], config["proxy"], config["isNested"], config["workload_uri"]);
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
            if (managementUri.EndsWith(".sock"))
            {
                string response = GetSocket.GetSocketResponse(managementUri.TrimEnd('/'), "/modules/?api-version=2018-06-28");

                if (!response.StartsWith("HTTP/1.1 200 OK"))
                {
                    throw new Exception($"Got bad response: {response}");
                }
            }
            else
            {
                using (var http = new HttpClient())
                using (var response = await http.GetAsync(managementUri.TrimEnd('/') + "/modules/?api-version=2018-06-28"))
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        static async Task LoadTrustBundle(string workload_uri)
        {
            string dummy = "0";

            IEnumerable<X509Certificate2> trustBundle = await CertificateHelper.
                GetTrustBundleFromEdgelet(new Uri(workload_uri), dummy, CLIENT_WORKLOAD_API_VERSION, dummy, dummy);
            CertificateHelper.InstallCertificates(trustBundle, null);
        }

        static async Task Upstream(string hostname, string port, string proxy, string isNested, string workload_uri)
        {
            bool nested = string.Equals(isNested, "true");

            if (port == "443")
            {
                var httpClientHandler = new HttpClientHandler();

                if (proxy != null)
                {
                    Environment.SetEnvironmentVariable("https_proxy", proxy);
                }

                if (nested)
                {
                    await LoadTrustBundle(workload_uri);
                }

                var httpClient = new HttpClient(httpClientHandler);
                var logsUrl = string.Format("https://{0}/devices/0000/modules", hostname);
                var httpRequest = new HttpRequestMessage(HttpMethod.Get, logsUrl);
                try
                {
                    HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
                    if (nested)
                    {
                        var keys = httpResponseMessage.Headers.GetValues("iothub-errorcode");
                        if (!keys.Contains("InvalidProtocolVersion"))
                        {
                            throw new Exception($"Wrong value for iothub-errorcode header");
                        }
                    }
                }
                catch (Exception ex)
                {
                    string message = ex.Message;
                    if ((ex.InnerException is AuthenticationException) && nested)
                    {
                        message += "Make sure that the parent root certificate is part of this device trustbundle. Use the command 'openssl s_client -connect parent_hostname:443' to display parent certificate chain.";
                    }

                    throw new Exception(message);
                }
            }
            else
            {
                if (proxy != null)
                {
                    Uri proxyUri = new Uri(proxy);

                    // Proxy Lib Nuget Package has a bug where which sends the  incorrect connect command. Untill this PR Gets merged https://github.com/grinay/ProxyLib/pull/1, Use a local implementation to send a connect command
                    using var tcpClient = new TcpClient();
                    tcpClient.SendTimeout = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;
                    tcpClient.ReceiveTimeout = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;

                    var connectTask = tcpClient.ConnectAsync(proxyUri.Host, proxyUri.Port);
                    await TaskEx.TimeoutAfter(connectTask, TimeSpan.FromMilliseconds(SOCKET_STREAM_WAIT_TIMEOUT_MS));

                    NetworkStream stream = tcpClient.GetStream();
                    string connectCmd = string.Format(CultureInfo.InvariantCulture, "CONNECT {0}:{1} HTTP/1.0\r\nHOST: {0}:{1}\r\n\r\n", proxyUri.Host, proxyUri.Port.ToString(CultureInfo.InvariantCulture));
                    byte[] request = ASCIIEncoding.ASCII.GetBytes(connectCmd);

                    // send the connect request
                    stream.Write(request, 0, request.Length);

                    await WaitForData(stream);

                    byte[] response = new byte[tcpClient.ReceiveBufferSize];
                    StringBuilder sbuilder = new StringBuilder();
                    var streamData = new StreamReader(stream).ReadToEnd();
                    ParseResponse(streamData);
                }
                else
                {
                    TcpClient client = new TcpClient();
                    var connectTask = client.ConnectAsync(hostname, int.Parse(port));
                    await TaskEx.TimeoutAfter(connectTask, TimeSpan.FromMilliseconds(SOCKET_STREAM_WAIT_TIMEOUT_MS));
                    client.GetStream();
                }
            }
        }

        static void ParentHostname(string parent_hostname)
        {
            _ = Dns.GetHostEntry(parent_hostname);
        }

        static async Task WaitForData(NetworkStream stream)
        {
            int sleepTime = 0;
            while (!stream.DataAvailable)
            {
                await Task.Delay(SOCKET_STREAM_WAIT_INTERVAL_MS);
                sleepTime += SOCKET_STREAM_WAIT_INTERVAL_MS;
                if (sleepTime > SOCKET_STREAM_WAIT_TIMEOUT_MS)
                {
                    throw new InvalidOperationException("Timed out while waiting for Data from TCP Socket Stream");
                }
            }
        }

        static void ParseResponse(string response)
        {
            string[] data = null;

            // Get rid of the LF character if it exists and then split the string on all CR
            data = response.Replace('\n', ' ').Split('\r');
            (var code, var text) = ParseCodeAndText(data[0]);
            if (code != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Response {response} Returned a Non-Sucess Status Code");
            }
        }

        static (HttpStatusCode code, string text) ParseCodeAndText(string line)
        {
            int begin = 0;
            int end = 0;
            string val = null;

            if (line.IndexOf("HTTP") == -1)
                throw new ArgumentException(string.Format("No HTTP response received from proxy destination.  Server response: {0}.", line));

            begin = line.IndexOf(" ") + 1;
            end = line.IndexOf(" ", begin);

            val = line.Substring(begin, end - begin);

            if (!int.TryParse(val, out int code))
            {
                throw new ArgumentException(string.Format("An invalid response code was received from proxy destination.  Server response: {0}.", line));
            }

            var respCode = (HttpStatusCode)code;
            var respText = line.Substring(end + 1).Trim();
            return (respCode, respText);
        }
    }
}
