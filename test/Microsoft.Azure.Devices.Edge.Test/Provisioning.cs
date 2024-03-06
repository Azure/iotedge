// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;

    [EndToEnd]
    public class Provisioning : DeviceProvisioningFixture
    {
        protected readonly IotHub iotHub;

        public Provisioning()
        {
            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.TestRunnerProxy);
        }

        string DeriveDeviceKey(byte[] groupKey, string deviceId)
        {
            using (var hmac = new HMACSHA256(groupKey))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(deviceId)));
            }
        }

        [Test]
        [Category("CentOsSafe")]
        [Category("FlakyOnArm")]
        public async Task DpsSymmetricKey()
        {
            string idScope = Context.Current.DpsIdScope.Expect(() =>
                new InvalidOperationException("Missing DPS ID scope (check dpsIdScope in context.json)"));
            string groupKey = Context.Current.DpsGroupKey.Expect(() =>
                new InvalidOperationException("Missing DPS enrollment group key (check DPS_GROUP_KEY env var)"));
            string deviceId = DeviceId.Current.Generate();

            string deviceKey = this.DeriveDeviceKey(Convert.FromBase64String(groupKey), deviceId);

            CancellationToken token = this.TestToken;

            (var certs, _) = await TestCertificates.GenerateEdgeCaCertsAsync(
                deviceId,
                this.daemon.GetCertificatesPath(),
                token);

            await this.daemon.ConfigureAsync(
                async config =>
                {
                    config.SetCertificates(certs);
                    config.SetDpsSymmetricKey(idScope, deviceId, deviceKey);
                    await config.UpdateAsync(token);
                    return ("with DPS symmetric key attestation for '{Identity}'", new object[] { deviceId });
                },
                token);

            var agent = new EdgeAgent(deviceId, this.iotHub);
            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, this.cli, token);
            await agent.PingAsync(token);

            Option<EdgeDevice> device = await EdgeDevice.GetIdentityAsync(
                deviceId,
                this.iotHub,
                token,
                takeOwnership: true);

            Context.Current.DeleteList.TryAdd(
                deviceId,
                device.Expect(() => new InvalidOperationException(
                    $"Device '{deviceId}' should have been created by DPS, but was not found in '{this.iotHub.Hostname}'")));
        }

        [Test]
        [Category("FlakyOnArm")]
        public async Task DpsX509()
        {
            string idScope = Context.Current.DpsIdScope.Expect(() =>
                new InvalidOperationException("Missing DPS ID scope (check dpsIdScope in context.json)"));
            string deviceId = DeviceId.Current.Generate();

            CancellationToken token = this.TestToken;

            var certsPath = this.daemon.GetCertificatesPath();
            var idCerts = await TestCertificates.GenerateIdentityCertificatesAsync(deviceId, certsPath, token);
            (var edgeCaCerts, _) = await TestCertificates.GenerateEdgeCaCertsAsync(deviceId, certsPath, token);

            await this.daemon.ConfigureAsync(
                async config =>
                {
                    config.SetCertificates(edgeCaCerts);
                    config.SetDpsX509(idScope, idCerts.CertificatePath, idCerts.KeyPath);
                    await config.UpdateAsync(token);
                    return ("with DPS X509 attestation for '{Identity}'", new object[] { deviceId });
                },
                token);

            var agent = new EdgeAgent(deviceId, this.iotHub);
            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, this.cli, token);
            await agent.PingAsync(token);

            Option<EdgeDevice> device = await EdgeDevice.GetIdentityAsync(
                deviceId,
                this.iotHub,
                token,
                takeOwnership: true);

            Context.Current.DeleteList.TryAdd(
                deviceId,
                device.Expect(() => new InvalidOperationException(
                    $"Device '{deviceId}' should have been created by DPS, but was not found in '{this.iotHub.Hostname}'")));
        }
    }
}
