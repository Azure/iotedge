// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Serilog;

    [EndToEnd]
    class Device : SasManualProvisioningFixture
    {
        [Test]
        public async Task IotedgeCheck()
        {
            CancellationToken token = this.TestToken;

            const string configPath = "C:\\ProgramData\\iotedge-moby\\config\\daemon.json";
            if (File.Exists(configPath))
            {
                string before = await File.ReadAllTextAsync(configPath, token);
                Log.Information($">>> Contents of '{configPath}' before write:\n{before}");
            }

            // Add DNS settings to iotedge-moby config
            string config = JsonConvert.SerializeObject(new
            {
                dns = new[] { "1.1.1.1" }
            });

            await this.runtime.DeployConfigurationAsync(token);

            File.WriteAllText("C:\\ProgramData\\iotedge-moby\\config\\daemon.json", config);
            string after = await File.ReadAllTextAsync(configPath, token);
            Log.Information($">>> Contents of '{configPath}' after write:\n{after}");

            string[] output = null;
            try
            {
                output = await Process.RunAsync(
                    "iotedge",
                    new[]
                    {
                        "check",
                        "--diagnostics-image-name",
                        "mcr.microsoft.com/azureiotedge-diagnostics:1.0.10-rc1",
                        "--dont-run",
                        "iotedged-version",
                        "certificates-quickstart",
                        "container-engine-logrotate",
                        "edge-agent-storage-mounted-from-host",
                        "edge-hub-storage-mounted-from-host"
                    }.Join(" "),
                    token);
            }
            catch (Exception e)
            {
                Log.Information($"iotedge check failed with error:\n{e.ToString()}");
            }

            Log.Information($">>> iotedge check results:\n{output?.Join("\n") ?? "<none>"}");
        }

        [Test]
        [Category("CentOsSafe")]
        public async Task QuickstartCerts()
        {
            CancellationToken token = this.TestToken;

            await this.runtime.DeployConfigurationAsync(token);

            string leafDeviceId = DeviceId.Current.Generate();

            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                Protocol.Amqp,
                AuthenticationType.Sas,
                Option.None<string>(),
                false,
                CertificateAuthority.GetQuickstart(),
                this.iotHub,
                token,
                Option.None<string>());

            await TryFinally.DoAsync(
                async () =>
                {
                    DateTime seekTime = DateTime.Now;
                    await leaf.SendEventAsync(token);
                    await leaf.WaitForEventsReceivedAsync(seekTime, token);
                    await leaf.InvokeDirectMethodAsync(token);
                },
                async () =>
                {
                    await leaf.DeleteIdentityAsync(token);
                });
        }
    }
}
