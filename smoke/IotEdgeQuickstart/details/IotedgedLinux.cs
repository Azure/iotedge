// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System;
    using System.ComponentModel;
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

        public HttpUris()
            : this(GetIpAddress())
        {
        }

        public HttpUris(string hostname)
        {
            this.ConnectManagement = $"http://{hostname}:{ManagementPort}";
            this.ConnectWorkload = $"http://{hostname}:{WorkloadPort}";
            this.ListenManagement = $"http://0.0.0.0:{ManagementPort}";
            this.ListenWorkload = $"http://0.0.0.0:{WorkloadPort}";
        }

        public string ConnectManagement { get; }

        public string ConnectWorkload { get; }

        public string ListenManagement { get; }

        public string ListenWorkload { get; }

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

    class IotedgedLinux : IBootstrapper
    {
        readonly string archivePath;
        readonly Option<RegistryCredentials> credentials;
        readonly Option<HttpUris> httpUris;
        readonly Option<string> proxy;
        readonly Option<UpstreamProtocolType> upstreamProtocol;

        public IotedgedLinux(string archivePath, Option<RegistryCredentials> credentials, Option<HttpUris> httpUris, Option<string> proxy, Option<UpstreamProtocolType> upstreamProtocol)
        {
            this.archivePath = archivePath;
            this.credentials = credentials;
            this.httpUris = httpUris;
            this.proxy = proxy;
            this.upstreamProtocol = upstreamProtocol;
        }

        public async Task VerifyNotActive()
        {
            string[] result = await Process.RunAsync("bash", "-c \"systemctl --no-pager show iotedge | grep ActiveState=\"");
            if (result.First().Split("=").Last() == "active")
            {
                throw new Exception("IoT Edge Security Daemon is already active. If you want this test to overwrite the active configuration, please run `systemctl stop iotedge` first.");
            }
        }

        public Task VerifyDependenciesAreInstalled() => Task.CompletedTask;

        public async Task VerifyModuleIsRunning(string name)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20))) // This long timeout is needed for resource constrained devices pulling the large tempFilterFunctions image
            {
                string errorMessage = null;

                try
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

                        string options = this.httpUris.Match(uris => $"-H {uris.ConnectManagement} ", () => string.Empty);

                        try
                        {
                            string[] result = await Process.RunAsync(
                                "iotedge",
                                $"{options}list",
                                cts.Token);

                            string status = result
                                .Where(ln => ln.Split(null as char[], StringSplitOptions.RemoveEmptyEntries).First() == name)
                                .DefaultIfEmpty("name status")
                                .Single()
                                .Split(null as char[], StringSplitOptions.RemoveEmptyEntries)
                                .ElementAt(1); // second column is STATUS

                            if (status == "running")
                            {
                                break;
                            }

                            errorMessage = "Not found";
                        }
                        catch (Win32Exception e)
                        {
                            Console.WriteLine($"Error searching for {name} module: {e.Message}. Retrying.");
                        }
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

            string commandName;
            string commandArgs;

            // Use apt-get if a package name is given, or dpkg if a package file is given.
            // We'd like to use apt-get for both cases, but older versions of apt-get (e.g.,
            // in Raspbian) can't accept a package file.
            if (string.IsNullOrEmpty(this.archivePath))
            {
                commandName = "apt-get";
                commandArgs = $"--yes install {PackageName}";
            }
            else
            {
                commandName = "dpkg";
                commandArgs = $"--force-confnew -i {this.archivePath}";
            }

            return Process.RunAsync(
                commandName,
                commandArgs,
                300); // 5 min timeout because install can be slow on raspberry pi
        }

        public async Task Configure(DeviceProvisioningMethod method, string image, string hostname, string deviceCaCert, string deviceCaPk, string deviceCaCerts, LogLevel runtimeLogLevel)
        {
            Console.WriteLine($"Setting up iotedged with agent image '{image}'");

            const string YamlPath = "/etc/iotedge/config.yaml";
            Task<string> text = File.ReadAllTextAsync(YamlPath);
            var doc = new YamlDocument(await text);

            method.ManualConnectionString.Match(
                cs =>
                {
                    doc.ReplaceOrAdd("provisioning.device_connection_string", cs);
                    return string.Empty;
                },
                () =>
                {
                    doc.Remove("provisioning.device_connection_string");
                    return string.Empty;
                });

            method.Dps.ForEach(
                dps =>
                {
                    doc.ReplaceOrAdd("provisioning.source", "dps");
                    doc.ReplaceOrAdd("provisioning.global_endpoint", dps.EndPoint);
                    doc.ReplaceOrAdd("provisioning.scope_id", dps.ScopeId);
                    switch (dps.AttestationType)
                    {
                        case DPSAttestationType.SymmetricKey:
                            doc.ReplaceOrAdd("provisioning.attestation.method", "symmetric_key");
                            doc.ReplaceOrAdd("provisioning.attestation.symmetric_key", dps.SymmetricKey.Expect(() => new ArgumentException("Expected symmetric key")));
                            break;
                        case DPSAttestationType.X509:
                            var certUri = new Uri(dps.DeviceIdentityCertificate.Expect(() => new ArgumentException("Expected path to identity certificate")));
                            var keyUri = new Uri(dps.DeviceIdentityPrivateKey.Expect(() => new ArgumentException("Expected path to identity private key")));
                            doc.ReplaceOrAdd("provisioning.attestation.method", "x509");
                            doc.ReplaceOrAdd("provisioning.attestation.identity_cert", certUri.AbsoluteUri);
                            doc.ReplaceOrAdd("provisioning.attestation.identity_pk", keyUri.AbsoluteUri);
                            break;
                        default:
                            doc.ReplaceOrAdd("provisioning.attestation.method", "tpm");
                            break;
                    }

                    dps.RegistrationId.ForEach(id => { doc.ReplaceOrAdd("provisioning.attestation.registration_id", id); });
                });

            doc.ReplaceOrAdd("agent.config.image", image);
            doc.ReplaceOrAdd("hostname", hostname);

            foreach (RegistryCredentials c in this.credentials)
            {
                doc.ReplaceOrAdd("agent.config.auth.serveraddress", c.Address);
                doc.ReplaceOrAdd("agent.config.auth.username", c.User);
                doc.ReplaceOrAdd("agent.config.auth.password", c.Password);
            }

            doc.ReplaceOrAdd("agent.env.RuntimeLogLevel", runtimeLogLevel.ToString());

            if (this.httpUris.HasValue)
            {
                HttpUris uris = this.httpUris.OrDefault();
                doc.ReplaceOrAdd("connect.management_uri", uris.ConnectManagement);
                doc.ReplaceOrAdd("connect.workload_uri", uris.ConnectWorkload);
                doc.ReplaceOrAdd("listen.management_uri", uris.ListenManagement);
                doc.ReplaceOrAdd("listen.workload_uri", uris.ListenWorkload);
            }
            else
            {
                doc.ReplaceOrAdd("connect.management_uri", "unix:///var/run/iotedge/mgmt.sock");
                doc.ReplaceOrAdd("connect.workload_uri", "unix:///var/run/iotedge/workload.sock");
                doc.ReplaceOrAdd("listen.management_uri", "fd://iotedge.mgmt.socket");
                doc.ReplaceOrAdd("listen.workload_uri", "fd://iotedge.socket");
            }

            if (!string.IsNullOrEmpty(deviceCaCert) && !string.IsNullOrEmpty(deviceCaPk) && !string.IsNullOrEmpty(deviceCaCerts))
            {
                doc.ReplaceOrAdd("certificates.device_ca_cert", deviceCaCert);
                doc.ReplaceOrAdd("certificates.device_ca_pk", deviceCaPk);
                doc.ReplaceOrAdd("certificates.trusted_ca_certs", deviceCaCerts);
            }

            this.proxy.ForEach(proxy => doc.ReplaceOrAdd("agent.env.https_proxy", proxy));

            this.upstreamProtocol.ForEach(upstreamProtocol => doc.ReplaceOrAdd("agent.env.UpstreamProtocol", upstreamProtocol.ToString()));

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
                        if (result.First().Split("=").Last() == "active")
                        {
                            break;
                        }

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
