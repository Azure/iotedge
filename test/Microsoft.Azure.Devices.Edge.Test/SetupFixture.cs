// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using NUnit.Framework;
    using Serilog;
    using Serilog.Events;

    [SetUpFixture]
    public class SetupFixture
    {
        IEdgeDaemon daemon;

        private (string, string)[] configFiles =
        {
            ("/etc/aziot/keyd/config.toml", "aziotks"),
            ("/etc/aziot/certd/config.toml", "aziotcs"),
            ("/etc/aziot/identityd/config.toml", "aziotid"),
            ("/etc/aziot/tpmd/config.toml", "aziotts"),
            ("/etc/aziot/edged/config.toml", "iotedge")
        };

        [OneTimeSetUp]
        public async Task BeforeAllAsync()
        {
            using var cts = new CancellationTokenSource(Context.Current.SetupTimeout);
            CancellationToken token = cts.Token;

            this.daemon = await OsPlatform.Current.CreateEdgeDaemonAsync(
                Context.Current.InstallerPath,
                token);

            await Profiler.Run(
                async () =>
                {
                    // Set up logging
                    LogEventLevel consoleLevel = Context.Current.Verbose
                        ? LogEventLevel.Verbose
                        : LogEventLevel.Information;
                    var loggerConfig = new LoggerConfiguration()
                        .MinimumLevel.Verbose()
                        .WriteTo.NUnit(consoleLevel);
                    Context.Current.LogFile.ForEach(f => loggerConfig.WriteTo.File(f));
                    Log.Logger = loggerConfig.CreateLogger();

                    // Install IoT Edge, and do some basic configuration
                    await this.daemon.UninstallAsync(token);
                    await this.daemon.InstallAsync(Context.Current.PackagePath, Context.Current.EdgeProxy, token);

                    // Clean the directory for test certs, keys, etc.
                    if (Directory.Exists(FixedPaths.E2E_TEST_DIR))
                    {
                        Directory.Delete(FixedPaths.E2E_TEST_DIR, true);
                    }

                    Directory.CreateDirectory(FixedPaths.E2E_TEST_DIR);

                    // Backup any existing service config files.
                    foreach ((string file, string owner) in this.configFiles)
                    {
                        if (File.Exists(file))
                        {
                            File.Move(file, file + ".backup", true);
                        }

                        // Reset all config files to the default file.
                        ResetConfigFile(file, file + ".default", owner);
                    }

                    await this.daemon.ConfigureAsync(
                        config =>
                        {
                            var msgBuilder = new StringBuilder();
                            var props = new List<object>();

                            string hostname = Context.Current.Hostname.GetOrElse(Dns.GetHostName());
                            config.SetDeviceHostname(hostname);
                            msgBuilder.Append("with hostname '{hostname}'");
                            props.Add(hostname);
                            Log.Information("Search parents");
                            Context.Current.ParentHostname.ForEach(parentHostname =>
                            {
                                Log.Information($"Found parent hostname {parentHostname}");
                                config.SetParentHostname(parentHostname);
                                msgBuilder.AppendLine($", parent hostname '{parentHostname}'");
                                props.Add(parentHostname);

                                string edgeAgent = Regex.Replace(Context.Current.EdgeAgentImage.GetOrElse(string.Empty), @"\$upstream", parentHostname);
                                config.SetEdgeAgentImage(edgeAgent);
                            });

                            Context.Current.EdgeProxy.ForEach(proxy =>
                            {
                                config.AddHttpsProxy(proxy);
                                msgBuilder.AppendLine(", proxy '{ProxyUri}'");
                                props.Add(proxy.ToString());
                            });

                            config.Update();

                            return Task.FromResult((msgBuilder.ToString(), props.ToArray()));
                        },
                        token,
                        restart: false);
                },
                "Completed end-to-end test setup");
        }

        [OneTimeTearDown]
        public Task AfterAllAsync() => TryFinally.DoAsync(
            () => Profiler.Run(
                async () =>
                {
                    using var cts = new CancellationTokenSource(Context.Current.TeardownTimeout);
                    CancellationToken token = cts.Token;
                    await this.daemon.StopAsync(token);
                    foreach (EdgeDevice device in Context.Current.DeleteList.Values)
                    {
                        await device.MaybeDeleteIdentityAsync(token);
                    }

                    // Remove packages installed by this run.
                    await this.daemon.UninstallAsync(token);

                    // Delete test certs, keys, etc.
                    Directory.Delete(FixedPaths.E2E_TEST_DIR, true);
                },
                "Completed end-to-end test teardown"),
            () =>
            {
                Log.CloseAndFlush();
            });

        private static void ResetConfigFile(string configFile, string defaultFile, string owner)
        {
            // Reset the config file to the default.
            Log.Information($"Resetting {configFile} to {defaultFile}");
            File.Copy(defaultFile, configFile, true);
            OsPlatform.Current.SetOwner(configFile, owner, "644");

            // Clear existing principals.
            string principalsPath = Path.Combine(
                Path.GetDirectoryName(configFile),
                "config.d");

            if (Directory.Exists(principalsPath))
            {
                Directory.Delete(principalsPath, true);

                Directory.CreateDirectory(principalsPath);
                OsPlatform.Current.SetOwner(principalsPath, owner, "755");
                Log.Information($"Cleared {principalsPath}");
            }
        }
    }

    // Generates a test CA cert, test CA key, and trust bundle.
    // TODO: Remove this once iotedge init is finished?
    public class TestCertificates
    {
        private string deviceId;
        private CaCertificates certs;

        TestCertificates(string deviceId, CaCertificates certs)
        {
            this.deviceId = deviceId;
            this.certs = certs;
        }

        public static async Task<(TestCertificates, CertificateAuthority ca)> GenerateCertsAsync(string deviceId, CancellationToken token)
        {
            string scriptPath = Context.Current.CaCertScriptPath.Expect(
                () => new System.InvalidOperationException("Missing CA cert script path"));
            (string, string, string) rootCa = Context.Current.RootCaKeys.Expect(
                () => new System.InvalidOperationException("Missing root CA"));

            CertificateAuthority ca = await CertificateAuthority.CreateAsync(deviceId, rootCa, scriptPath, token);
            CaCertificates certs = await ca.GenerateCaCertificatesAsync(deviceId, token);
            ca.EdgeCertificates = certs;

            return (new TestCertificates(deviceId, certs), ca);
        }

        public void AddCertsToConfig(DaemonConfiguration config)
        {
            string path = Path.Combine(FixedPaths.E2E_TEST_DIR, this.deviceId);
            string certPath = Path.Combine(path, "device_ca_cert.pem");
            string keyPath = Path.Combine(path, "device_ca_cert_key.pem");
            string trustBundlePath = Path.Combine(path, "trust_bundle.pem");

            Directory.CreateDirectory(path);
            File.Copy(this.certs.TrustedCertificatesPath, trustBundlePath);
            OsPlatform.Current.SetOwner(trustBundlePath, "aziotcs", "644");
            File.Copy(this.certs.CertificatePath, certPath);
            OsPlatform.Current.SetOwner(certPath, "aziotcs", "644");
            File.Copy(this.certs.KeyPath, keyPath);
            OsPlatform.Current.SetOwner(keyPath, "aziotks", "600");

            config.SetCertificates(new CaCertificates(certPath, keyPath, trustBundlePath));
        }
    }
}
