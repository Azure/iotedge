// Copyright (c) Microsoft. All rights reserved.

namespace IotEdgeQuickstart.Details
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class HttpUris
    {
        const int ManagementPort = 15580;
        const int WorkloadPort = 15581;

        public string ConnectManagement { get; }
        public string ConnectWorkload { get; }
        public string ListenManagement { get; }
        public string ListenWorkload { get; }

        public HttpUris() : this(GetIpAddress()) {}

        public HttpUris(string hostname)
        {
            this.ConnectManagement = $"http://{hostname}:{ManagementPort}";
            this.ConnectWorkload = $"http://{hostname}:{WorkloadPort}";
            this.ListenManagement = $"http://0.0.0.0:{ManagementPort}";
            this.ListenWorkload = $"http://0.0.0.0:{WorkloadPort}";
        }

        static string GetIpAddress()
        {
            // TODO: should use an internal IP address--e.g. docker0's address--instead
            //       of the public-facing address. The output of this command would be
            //       a good candidate:
            //       docker network inspect --format='{{(index .IPAM.Config 0).Gateway}}' bridge
            const string Server = "microsoft.com";
            const int Port = 443;

            IPHostEntry entry = Dns.GetHostEntry(Server);

            foreach (IPAddress address in entry.AddressList)
            {
                var endpoint = new IPEndPoint(address, Port);
                using (var s = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                {
                    s.Connect(endpoint);
                    if (s.Connected)
                    {
                        return (s.LocalEndPoint as IPEndPoint)?.Address.ToString();
                    }
                }
            }

            return string.Empty;
        }
    }

    class Iotedged : IBootstrapper
    {
        readonly string archivePath;
        readonly Option<RegistryCredentials> credentials;
        readonly Option<HttpUris> httpUris;

        public Iotedged(string archivePath, Option<RegistryCredentials> credentials, Option<HttpUris> httpUris)
        {
            this.archivePath = archivePath;
            this.credentials = credentials;
            this.httpUris = httpUris;
        }

        public async Task VerifyNotActive()
        {
            // 'sleep' before 'systemctl' works around a problem in
            // RunProcessAsTask seen on Raspberry Pi 3. See
            // https://github.com/jamesmanning/RunProcessAsTask/issues/20
            string[] result = await Process.RunAsync("bash", "-c \"sleep .5 && systemctl --no-pager show iotedge | grep ActiveState=\"");
            if (result.First().Split("=").Last() == "active")
            {
                throw new Exception("IoT Edge Security Daemon is already active. If you want this test to overwrite the active configuration, please run `systemctl stop iotedged` first.");
            }
        }

        public Task VerifyDependenciesAreInstalled() => Task.CompletedTask;

        public async Task VerifyModuleIsRunning(string name)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                string errorMessage = null;

                try
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

                        string options = this.httpUris.Match(uris => $"-H {uris.ConnectManagement} ", () => string.Empty);

                        string[] result = await Process.RunAsync(
                            "iotedge",
                            $"{options}list",
                            cts.Token);

                        string status = result
                            .Where(ln => ln.Split(null as char[], StringSplitOptions.RemoveEmptyEntries).First() == name)
                            .DefaultIfEmpty("name status")
                            .Single()
                            .Split(null as char[], StringSplitOptions.RemoveEmptyEntries)
                            .ElementAt(1);  // second column is STATUS

                        if (status == "running") break;

                        errorMessage = "Not found";
                    }
                }
                catch (OperationCanceledException e)
                {
                    throw new Exception($"Error searching for {name} module: {errorMessage ?? e.Message}");
                }
                catch (Exception e)
                {
                    throw new Exception($"Error searching for {name} module: {e.Message}");
                }
            }
        }

        public Task Install()
        {
            const string PackageName = "iotedge";

            Console.WriteLine($"Installing debian package '{PackageName}' from {this.archivePath ?? "apt"}");

            // Would be nice to use 'apt-get --yes install' instead of 'dpkg -i' because
            // apt-get automatically installs dependencies, but Raspbian's version of
            // apt-get doesn't support .deb files as arguments
            return Process.RunAsync(
                "dpkg",
                $"-i {this.archivePath ?? PackageName}",
                300); // 5 min timeout because install can be slow on raspberry pi
        }

        public async Task Configure(string connectionString, string image, string hostname)
        {
            Console.WriteLine($"Setting up iotedged with agent image '{image}'");

            const string YamlPath = "/etc/iotedge/config.yaml";
            Task<string> text = File.ReadAllTextAsync(YamlPath);

            var doc = new YamlDocument(await text);
            doc.Replace("provisioning.device_connection_string", connectionString);
            doc.Replace("agent.config.image", image);
            doc.Replace("hostname", hostname);

            foreach (RegistryCredentials c in this.credentials)
            {
                doc.Replace("agent.config.auth.serveraddress", c.Address);
                doc.Replace("agent.config.auth.username", c.User);
                doc.Replace("agent.config.auth.password", c.Password);
            }

            if (this.httpUris.HasValue)
            {
                HttpUris uris = this.httpUris.OrDefault();
                doc.Replace("connect.management_uri", uris.ConnectManagement);
                doc.Replace("connect.workload_uri", uris.ConnectWorkload);
                doc.Replace("listen.management_uri", uris.ListenManagement);
                doc.Replace("listen.workload_uri", uris.ListenWorkload);
            }
            else
            {
                doc.Replace("connect.management_uri", "unix:///var/run/iotedge/mgmt.sock");
                doc.Replace("connect.workload_uri", "unix:///var/run/iotedge/workload.sock");
                doc.Replace("listen.management_uri", "fd://iotedge.mgmt.socket");
                doc.Replace("listen.workload_uri", "fd://iotedge.socket");
            }

            string result = doc.ToString();

            FileAttributes attr = 0;
            if (File.Exists(YamlPath))
            {
                attr = File.GetAttributes(YamlPath);
                File.SetAttributes(YamlPath, attr & ~FileAttributes.ReadOnly);
            }

            await File.WriteAllTextAsync(YamlPath, result);

            if (attr != 0)
            {
                File.SetAttributes(YamlPath, attr);
            }
        }

        public async Task Start()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
            {
                string errorMessage = null;

                try
                {
                    await Process.RunAsync("systemctl", "enable iotedge", cts.Token);
                    await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
                    await Process.RunAsync("systemctl", "restart iotedge", cts.Token);

                    // Wait for service to become active
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
                        string[] result = await Process.RunAsync("bash", "-c \"systemctl --no-pager show iotedge | grep ActiveState=\"");
                        if (result.First().Split("=").Last() == "active") break;
                        errorMessage = result.First();
                    }
                }
                catch (OperationCanceledException e)
                {
                    throw new Exception($"Error starting iotedged: {errorMessage ?? e.Message}");
                }
            }
        }

        public async Task Stop()
        {
            // Raspbian's systemctl doesn't support 'disable --now', so do
            // 'disable' + 'stop' instead
            await Process.RunAsync("systemctl", "disable iotedge", 60);
            await Process.RunAsync("systemctl", "stop iotedge", 60);
        }

        public Task Reset() => Task.CompletedTask;
    }
}
