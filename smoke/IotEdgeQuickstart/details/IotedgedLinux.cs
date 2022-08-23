// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
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

    interface ILinuxPackageInstall
    {
        public Task Install();
        public Task FindPackage(string packageName);
        public Task RemovePackage(string packageName);
    }

    class LinuxPackageInstallDep : ILinuxPackageInstall
    {
        readonly string archivePath;
        public LinuxPackageInstallDep(string archivePath)
        {
            this.archivePath = archivePath;
        }

        public Task Install()
        {
            string[] packages = Directory.GetFiles(this.archivePath, "*.deb");

            foreach (string package in packages)
            {
                Console.WriteLine($"Will install {package}");
            }

            string packageArguments = string.Join(" ", packages);

            return Process.RunAsync(
                "apt-get",
                $"install -y {packageArguments}",
                300); // 5 min timeout because install can be slow on raspberry pi
        }

        public Task FindPackage(string packageName)
        {
            return Process.RunAsync("bash", $"-c \"dpkg -l | grep {packageName}\"");
        }

        public Task RemovePackage(string packageName)
        {
            return Process.RunAsync("apt", $"purge -y {packageName}", 180);
        }
    }

    class LinuxPackageInstallRPM : ILinuxPackageInstall
    {
        readonly string archivePath;
        public LinuxPackageInstallRPM(string archivePath)
        {
            this.archivePath = archivePath;
        }

        public Task Install()
        {
            string[] packages = Directory.GetFiles(this.archivePath, "*.rpm");
            return Process.RunAsync(
                    "rpm",
                    $"--nodeps -i {string.Join(' ', packages)}",
                    300);
        }

        public Task FindPackage(string packageName)
        {
            return Process.RunAsync("bash", $"-c \"rpm -qa | grep {packageName}\"");
        }

        public Task RemovePackage(string packageName)
        {
            return Process.RunAsync("rpm", $"-e {packageName}", 180);
        }
    }

    class LinuxPackageNonInstall : ILinuxPackageInstall
    {
        public Task Install()
        {
            Console.WriteLine("Skipping installation of aziot-edge and aziot-identity-service.");
            return Task.CompletedTask;
        }

        public Task FindPackage(string packageName)
        {
            throw new Exception("Find package not permitted for non-installed packages");
        }

        public Task RemovePackage(string packageName)
        {
            throw new Exception("remove package not permitted for non-installed packages");
        }
    }

    class IotedgedLinux : IBootstrapper
    {
        const string KEYD = "/etc/aziot/keyd/config.toml";
        const string CERTD = "/etc/aziot/certd/config.toml";
        const string IDENTITYD = "/etc/aziot/identityd/config.toml";
        const string EDGED = "/etc/aziot/edged/config.toml";

        readonly Option<RegistryCredentials> credentials;
        readonly Option<HttpUris> httpUris;
        readonly UriSocks uriSocks;
        readonly Option<string> proxy;
        readonly Option<UpstreamProtocolType> upstreamProtocol;
        readonly bool overwritePackages;

        ILinuxPackageInstall installCommands;

        private struct Config
        {
            public string Owner;
            public string PrincipalsPath;
            public uint Uid;
            public TomlDocument Document;
        }

        public IotedgedLinux(Option<RegistryCredentials> credentials, Option<HttpUris> httpUris, UriSocks uriSocks, Option<string> proxy, Option<UpstreamProtocolType> upstreamProtocol, bool overwritePackages, ILinuxPackageInstall installCommands)
        {
            this.credentials = credentials;
            this.httpUris = httpUris;
            this.uriSocks = uriSocks;
            this.proxy = proxy;
            this.upstreamProtocol = upstreamProtocol;
            this.overwritePackages = overwritePackages;
            this.installCommands = installCommands;
        }

        public async Task UpdatePackageState()
        {
            string[] packages = new string[] { "aziot-edge", "aziot-identity-service", "iotedge", "libiothsm-std" };

            foreach (string package in packages)
            {
                try
                {
                    await this.installCommands.FindPackage(package);

                    if (this.overwritePackages)
                    {
                        Console.WriteLine($"{package}: found. Removing package.");
                        await this.installCommands.RemovePackage(package);
                    }
                    else
                    {
                        throw new Exception($"{package}: found. Not overwriting existing packages.");
                    }
                }
                catch (Win32Exception)
                {
                    Console.WriteLine($"{package}: not found.");
                }
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
            return this.installCommands.Install();
        }

        private static async Task<Config> InitConfig(string template, string owner)
        {
            Config config;
            string text = File.ReadAllText(template);

            config.Document = new TomlDocument(text);

            string principalsPath = Path.Combine(
                Path.GetDirectoryName(template),
                "config.d");

            if (Directory.Exists(principalsPath))
            {
                Directory.Delete(principalsPath, true);

                Directory.CreateDirectory(principalsPath);
                SetOwner(principalsPath, owner, "755");
                Console.WriteLine($"Cleared {principalsPath}");
            }

            config.PrincipalsPath = principalsPath;
            config.Owner = owner;
            config.Uid = await GetUid(owner);

            return config;
        }

        private void SetAuth(string keyName, Dictionary<string, Config> config)
        {
            // Grant Identity Service access to the provided device-id key and its master encryption key.
            this.AddAuthPrincipal(
                Path.Combine(config[KEYD].PrincipalsPath, "aziot-identityd-principal.toml"),
                config[KEYD].Owner,
                config[IDENTITYD].Uid,
                new string[] { keyName, "aziot_identityd_master_id" });

            // Grant aziot-edged access to device CA certs, server certs, and its master encryption key.
            this.AddIdentityPrincipal("aziot-edged", config[EDGED].Uid);
            this.AddAuthPrincipal(
                Path.Combine(config[KEYD].PrincipalsPath, "aziot-edged-principal.toml"),
                config[KEYD].Owner,
                config[EDGED].Uid,
                new string[] { "iotedge_master_encryption_id", "aziot-edged-ca" });
            this.AddAuthPrincipal(
                Path.Combine(config[CERTD].PrincipalsPath, "aziot-edged-principal.toml"),
                config[CERTD].Owner,
                config[EDGED].Uid,
                new string[] { "aziot-edged/module/*" });
        }

        public async Task Configure(
            DeviceProvisioningMethod method,
            Option<string> agentImage,
            string hostname,
            Option<string> parentHostname,
            string deviceCaCert,
            string deviceCaPk,
            string deviceCaCerts,
            LogLevel runtimeLogLevel)
        {
            agentImage.ForEach(
                image =>
                {
                    Console.WriteLine($"Setting up aziot-edged with agent image {image}");
                },
                () =>
                {
                    Console.WriteLine("Setting up aziot-edged with agent image 1.0");
                });

            // Initialize each service's config file.
            Dictionary<string, Config> config = new Dictionary<string, Config>();
            config.Add(KEYD, await InitConfig(KEYD + ".default", "aziotks"));
            config.Add(CERTD, await InitConfig(CERTD + ".default", "aziotcs"));
            config.Add(IDENTITYD, await InitConfig(IDENTITYD + ".default", "aziotid"));
            config.Add(EDGED, await InitConfig(EDGED + ".default", "iotedge"));

            // Directory for storing keys; create it if it doesn't exist.
            string keyDir = "/var/secrets/aziot/keyd/";
            Directory.CreateDirectory(keyDir);
            SetOwner(keyDir, config[KEYD].Owner, "700");

            // Need to always reprovision so previous test runs don't affect this one.
            config[EDGED].Document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");
            config[IDENTITYD].Document.RemoveIfExists("provisioning");
            parentHostname.ForEach(
                parent_hostame =>
                config[IDENTITYD].Document.ReplaceOrAdd("provisioning.local_gateway_hostname", parent_hostame));

            method.ManualConnectionString.Match(
                cs =>
                {
                    string keyPath = Path.Combine(keyDir, "device-id");
                    config[IDENTITYD].Document.ReplaceOrAdd("provisioning.source", "manual");
                    config[IDENTITYD].Document.ReplaceOrAdd("provisioning.authentication.method", "sas");
                    config[IDENTITYD].Document.ReplaceOrAdd("provisioning.authentication.device_id_pk", "device-id");
                    config[KEYD].Document.ReplaceOrAdd("preloaded_keys.device-id", $"file://{keyPath}");

                    string[] segments = cs.Split(";");

                    foreach (string s in segments)
                    {
                        string[] param = s.Split("=", 2);

                        switch (param[0])
                        {
                            case "HostName":
                                // replace IoTHub hostname with parent hostname for nested edge
                                config[IDENTITYD].Document.ReplaceOrAdd("provisioning.iothub_hostname", param[1]);
                                break;
                            case "SharedAccessKey":
                                File.WriteAllBytes(keyPath, Convert.FromBase64String(param[1]));
                                SetOwner(keyPath, config[KEYD].Owner, "600");
                                break;
                            case "DeviceId":
                                config[IDENTITYD].Document.ReplaceOrAdd("provisioning.device_id", param[1]);
                                break;
                            default:
                                break;
                        }
                    }

                    this.SetAuth("device-id", config);

                    return string.Empty;
                },
                () =>
                {
                    config[IDENTITYD].Document.RemoveIfExists("provisioning");
                    return string.Empty;
                });

            method.Dps.ForEach(
                dps =>
                {
                    config[IDENTITYD].Document.ReplaceOrAdd("provisioning.source", "dps");
                    config[IDENTITYD].Document.ReplaceOrAdd("provisioning.global_endpoint", dps.EndPoint);
                    config[IDENTITYD].Document.ReplaceOrAdd("provisioning.scope_id", dps.ScopeId);
                    switch (dps.AttestationType)
                    {
                        case DPSAttestationType.SymmetricKey:
                            string dpsKeyPath = Path.Combine(keyDir, "device-id");
                            string dpsKey = dps.SymmetricKey.Expect(() => new ArgumentException("Expected symmetric key"));

                            File.WriteAllBytes(dpsKeyPath, Convert.FromBase64String(dpsKey));
                            SetOwner(dpsKeyPath, config[KEYD].Owner, "600");

                            config[KEYD].Document.ReplaceOrAdd("preloaded_keys.device-id", new Uri(dpsKeyPath).AbsoluteUri);
                            config[IDENTITYD].Document.ReplaceOrAdd("provisioning.attestation.method", "symmetric_key");
                            config[IDENTITYD].Document.ReplaceOrAdd("provisioning.attestation.symmetric_key", "device-id");

                            this.SetAuth("device-id", config);

                            break;
                        case DPSAttestationType.X509:
                            string certPath = dps.DeviceIdentityCertificate.Expect(() => new ArgumentException("Expected path to identity certificate"));
                            string keyPath = dps.DeviceIdentityPrivateKey.Expect(() => new ArgumentException("Expected path to identity private key"));

                            SetOwner(certPath, config[CERTD].Owner, "444");
                            SetOwner(keyPath, config[KEYD].Owner, "400");

                            config[CERTD].Document.ReplaceOrAdd("preloaded_certs.device-id", new Uri(certPath).AbsoluteUri);
                            config[KEYD].Document.ReplaceOrAdd("preloaded_keys.device-id", new Uri(keyPath).AbsoluteUri);

                            config[IDENTITYD].Document.ReplaceOrAdd("provisioning.attestation.method", "x509");
                            config[IDENTITYD].Document.ReplaceOrAdd("provisioning.attestation.identity_cert", "device-id");
                            config[IDENTITYD].Document.ReplaceOrAdd("provisioning.attestation.identity_pk", "device-id");

                            this.SetAuth("device-id", config);

                            break;
                        default:
                            break;
                    }

                    dps.RegistrationId.ForEach(id => { config[IDENTITYD].Document.ReplaceOrAdd("provisioning.attestation.registration_id", id); });
                });

            agentImage.ForEach(image =>
            {
                config[EDGED].Document.ReplaceOrAdd("agent.config.image", image);
            });

            config[EDGED].Document.ReplaceOrAdd("hostname", hostname);
            config[IDENTITYD].Document.ReplaceOrAdd("hostname", hostname);

            parentHostname.ForEach(v => config[EDGED].Document.ReplaceOrAdd("parent_hostname", v));

            foreach (RegistryCredentials c in this.credentials)
            {
                config[EDGED].Document.ReplaceOrAdd("agent.config.auth.serveraddress", c.Address);
                config[EDGED].Document.ReplaceOrAdd("agent.config.auth.username", c.User);
                config[EDGED].Document.ReplaceOrAdd("agent.config.auth.password", c.Password);
            }

            config[EDGED].Document.ReplaceOrAdd("agent.env.RuntimeLogLevel", runtimeLogLevel.ToString());

            if (this.httpUris.HasValue)
            {
                HttpUris uris = this.httpUris.OrDefault();
                config[EDGED].Document.ReplaceOrAdd("connect.management_uri", uris.ConnectManagement);
                config[EDGED].Document.ReplaceOrAdd("connect.workload_uri", uris.ConnectWorkload);
                config[EDGED].Document.ReplaceOrAdd("listen.management_uri", uris.ListenManagement);
                config[EDGED].Document.ReplaceOrAdd("listen.workload_uri", uris.ListenWorkload);
            }
            else
            {
                UriSocks socks = this.uriSocks;
                config[EDGED].Document.ReplaceOrAdd("connect.management_uri", socks.ConnectManagement);
                config[EDGED].Document.ReplaceOrAdd("connect.workload_uri", socks.ConnectWorkload);
                config[EDGED].Document.ReplaceOrAdd("listen.management_uri", socks.ListenManagement);
                config[EDGED].Document.ReplaceOrAdd("listen.workload_uri", socks.ListenWorkload);
            }

            foreach (string file in new string[] { deviceCaCert, deviceCaPk, deviceCaCerts })
            {
                if (string.IsNullOrEmpty(file))
                {
                    throw new ArgumentException("device_ca_cert, device_ca_pk, and trusted_ca_certs must all be provided.");
                }

                if (!File.Exists(file))
                {
                    throw new ArgumentException($"{file} does not exist.");
                }
            }

            // Files must be readable by KS and CS users.
            SetOwner(deviceCaCerts, config[CERTD].Owner, "444");
            SetOwner(deviceCaCert, config[CERTD].Owner, "444");
            SetOwner(deviceCaPk, config[KEYD].Owner, "400");

            config[CERTD].Document.ReplaceOrAdd("preloaded_certs.aziot-edged-trust-bundle", new Uri(deviceCaCerts).AbsoluteUri);
            config[CERTD].Document.ReplaceOrAdd("preloaded_certs.aziot-edged-ca", new Uri(deviceCaCert).AbsoluteUri);
            config[KEYD].Document.ReplaceOrAdd("preloaded_keys.aziot-edged-ca", new Uri(deviceCaPk).AbsoluteUri);

            this.proxy.ForEach(proxy => config[EDGED].Document.ReplaceOrAdd("agent.env.https_proxy", proxy));

            this.upstreamProtocol.ForEach(upstreamProtocol => config[EDGED].Document.ReplaceOrAdd("agent.env.UpstreamProtocol", upstreamProtocol.ToString()));

            foreach (KeyValuePair<string, Config> service in config)
            {
                string path = service.Key;
                string text = service.Value.Document.ToString();

                await File.WriteAllTextAsync(path, text);
                SetOwner(path, service.Value.Owner, "644");
                Console.WriteLine($"Created config {path}");
            }

            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
            {
                Console.WriteLine($"Calling iotedge system set-log-level {runtimeLogLevel.ToString().ToLower()}");
                string[] output = await Process.RunAsync("iotedge", $"system set-log-level {runtimeLogLevel.ToString().ToLower()}", cts.Token);
                Console.WriteLine($"{output.ToString()}");
            }
        }

        public async Task Start()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
            {
                await Process.RunAsync("iotedge", "system restart", cts.Token);
                Console.WriteLine("Waiting for aziot-edged to start up.");

                // Waiting for the processes to enter the "Running" state doesn't guarantee that
                // they are fully started and ready to accept requests. Therefore, this function
                // must wait until a request can be processed.
                while (true)
                {
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "iotedge",
                        Arguments = "list",
                        RedirectStandardOutput = true
                    };
                    var request = System.Diagnostics.Process.Start(processInfo);

                    if (request.WaitForExit(1000))
                    {
                        if (request.ExitCode == 0)
                        {
                            request.Close();
                            Console.WriteLine("aziot-edged ready for requests.");
                            break;
                        }
                    }
                    else
                    {
                        request.Kill(true);
                        request.WaitForExit();
                        request.Close();
                        Console.WriteLine("aziot-edged not yet ready.");
                    }
                }
            }
        }

        public async Task Stop()
        {
            await Process.RunAsync("systemctl", "stop aziot-edged", 60);
            await Process.RunAsync("systemctl", "stop aziot-identityd", 60);
            await Process.RunAsync("systemctl", "stop aziot-tpmd", 60);
            await Process.RunAsync("systemctl", "stop aziot-certd", 60);
            await Process.RunAsync("systemctl", "stop aziot-keyd", 60);
        }

        public Task Reset() => Task.CompletedTask;

        private static async Task<uint> GetUid(string user)
        {
            string[] output = await Process.RunAsync("id", $"-u {user}");
            string uid = output[0].Trim();

            return System.Convert.ToUInt32(uid, 10);
        }

        private static void SetOwner(string path, string owner, string permissions)
        {
            var chown = System.Diagnostics.Process.Start("chown", $"{owner}:{owner} {path}");
            chown.WaitForExit();
            chown.Close();

            var chmod = System.Diagnostics.Process.Start("chmod", $"{permissions} {path}");
            chmod.WaitForExit();
            chmod.Close();
        }

        private void AddIdentityPrincipal(string name, uint uid, string[] type = null, Dictionary<string, string> opts = null)
        {
            string path = $"/etc/aziot/identityd/config.d/{name}-principal.toml";

            string principal = string.Join(
                "\n",
                "[[principal]]",
                $"uid = {uid}",
                $"name = \"{name}\"");

            if (type != null)
            {
                // Need to quote each type.
                for (int i = 0; i < type.Length; i++)
                {
                    type[i] = $"\"{type[i]}\"";
                }

                string types = string.Join(", ", type);
                principal = string.Join("\n", principal, $"idtype = [{types}]");
            }

            if (opts != null)
            {
                foreach (KeyValuePair<string, string> opt in opts)
                {
                    principal = string.Join("\n", principal, $"{opt.Key} = {opt.Value}");
                }
            }

            File.WriteAllText(path, principal + "\n");
            SetOwner(path, "aziotid", "644");
        }

        private void AddAuthPrincipal(string path, string owner, uint uid, string[] credentials)
        {
            if (credentials == null || credentials.Length == 0)
            {
                throw new ArgumentException("Empty array of credentials");
            }

            string auth = string.Empty;

            if (path.Contains("keyd"))
            {
                auth += "keys = [";
            }
            else if (path.Contains("certd"))
            {
                auth += "certs = [";
            }
            else
            {
                throw new ArgumentException("Invalid path for auth principal");
            }

            for (int i = 0; i < credentials.Length; i++)
            {
                credentials[i] = $"\"{credentials[i]}\"";
            }

            auth += string.Join(", ", credentials);
            auth += "]";

            string principal = string.Join(
                "\n",
                "[[principal]]",
                $"uid = {uid}",
                auth);

            File.WriteAllText(path, principal + "\n");
            SetOwner(path, owner, "644");
        }
    }
}
