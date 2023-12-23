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

        string DeriveDeviceKey(byte[] groupKey, string registrationId)
        {
            using (var hmac = new HMACSHA256(groupKey))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(registrationId)));
            }
        }

        [Test]
        [Category("CentOsSafe")]
        [Category("FlakyOnArm")]
        public async Task DpsSymmetricKey()
        {
            string idScope = Context.Current.DpsIdScope.Expect(() => new InvalidOperationException("Missing DPS ID scope (check dpsIdScope in context.json)"));
            string groupKey = Context.Current.DpsGroupKey.Expect(() => new InvalidOperationException("Missing DPS enrollment group key (check DPS_GROUP_KEY env var)"));
            string registrationId = DeviceId.Current.Generate();

            string deviceKey = this.DeriveDeviceKey(Convert.FromBase64String(groupKey), registrationId);

            CancellationToken token = this.TestToken;

            (TestCertificates testCerts, _) = await TestCertificates.GenerateCertsAsync(registrationId, token);

            await this.daemon.ConfigureAsync(
                async config =>
                {
                    config.SetCertificates(testCerts.CaCertificates);
                    config.SetDpsSymmetricKey(idScope, registrationId, deviceKey);
                    await config.UpdateAsync(token);
                    return ("with DPS symmetric key attestation for '{Identity}'", new object[] { registrationId });
                },
                token);

            var agent = new EdgeAgent(registrationId, this.iotHub);
            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, this.cli, token);
            await agent.PingAsync(token);

            Option<EdgeDevice> device = await EdgeDevice.GetIdentityAsync(
                registrationId,
                this.iotHub,
                token,
                takeOwnership: true);

            Context.Current.DeleteList.TryAdd(
                registrationId,
                device.Expect(() => new InvalidOperationException(
                    $"Device '{registrationId}' should have been created by DPS, but was not found in '{this.iotHub.Hostname}'")));
        }

        [Test]
        [Category("FlakyOnArm")]
        public async Task DpsX509()
        {
            (string, string, string) rootCa =
                        Context.Current.RootCaKeys.Expect(() => new InvalidOperationException("Missing DPS ID scope (check rootCaPrivateKeyPath in context.json)"));
            string caCertScriptPath =
                        Context.Current.CaCertScriptPath.Expect(() => new InvalidOperationException("Missing CA cert script path (check caCertScriptPath in context.json)"));
            string idScope = Context.Current.DpsIdScope.Expect(() => new InvalidOperationException("Missing DPS ID scope (check dpsIdScope in context.json)"));
            string registrationId = DeviceId.Current.Generate();

            CancellationToken token = this.TestToken;
            string destPath = Path.Combine(FixedPaths.E2E_TEST_DIR, registrationId);

            CertificateAuthority ca = await CertificateAuthority.CreateAsync(
                registrationId,
                rootCa,
                caCertScriptPath,
                token);

            IdCertificates idCert = await ca.GenerateIdentityCertificatesAsync(registrationId, destPath, token);
            (TestCertificates testCerts, _) = await TestCertificates.GenerateCertsAsync(registrationId, token);

            await this.daemon.ConfigureAsync(
                async config =>
                {
                    config.SetCertificates(testCerts.CaCertificates);
                    config.SetDpsX509(idScope, idCert.CertificatePath, idCert.KeyPath);
                    await config.UpdateAsync(token);
                    return ("with DPS X509 attestation for '{Identity}'", new object[] { registrationId });
                },
                token);

            var agent = new EdgeAgent(registrationId, this.iotHub);
            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, this.cli, token);
            await agent.PingAsync(token);

            Option<EdgeDevice> device = await EdgeDevice.GetIdentityAsync(
                registrationId,
                this.iotHub,
                token,
                takeOwnership: true);

            Context.Current.DeleteList.TryAdd(
                registrationId,
                device.Expect(() => new InvalidOperationException(
                    $"Device '{registrationId}' should have been created by DPS, but was not found in '{this.iotHub.Hostname}'")));
        }
    }
}
