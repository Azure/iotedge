// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;
    using Serilog;

    [EndToEnd]
    class Device : SasManualProvisioningFixture
    {
        [Test]
        public async Task IotedgeCheck()
        {
            CancellationToken token = this.TestToken;

            await this.runtime.DeployConfigurationAsync(token);

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

            Log.Information($">>> hostname: {Dns.GetHostName()}");

            Log.Information($">>> config.yaml\n{await File.ReadAllTextAsync("C:\\ProgramData\\iotedge\\config.yaml", token)}");
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
