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
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
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
                Context.Current.Proxy);
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
        public async Task DpsSymmetricKey()
        {
            string idScope = Context.Current.DpsIdScope.Expect(() => new InvalidOperationException("Missing DPS ID scope"));
            string groupKey = Context.Current.DpsGroupKey.Expect(() => new InvalidOperationException("Missing DPS enrollment group key"));
            string registrationId = DeviceId.Current.Generate();

            string deviceKey = this.DeriveDeviceKey(Convert.FromBase64String(groupKey), registrationId);

            CancellationToken token = this.TestToken;

            TestCertificates testCerts = await TestCertificates.GenerateCertsAsync(registrationId, token);

            await this.daemon.ConfigureAsync(
                config =>
                {
                    testCerts.AddCertsToConfig(config);
                    config.SetDpsSymmetricKey(idScope, registrationId, deviceKey);
                    config.Update();
                    return Task.FromResult((
                        "with DPS symmetric key attestation for '{Identity}'",
                        new object[] { registrationId }));
                },
                token);

            await this.daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

            var agent = new EdgeAgent(registrationId, this.iotHub);
            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
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
        public async Task DpsX509()
        {
            (string, string, string) rootCa =
                        Context.Current.RootCaKeys.Expect(() => new InvalidOperationException("Missing root CA keys"));
            string caCertScriptPath =
                        Context.Current.CaCertScriptPath.Expect(() => new InvalidOperationException("Missing CA cert script path"));
            string idScope = Context.Current.DpsIdScope.Expect(() => new InvalidOperationException("Missing DPS ID scope"));
            string registrationId = DeviceId.Current.Generate();

            CancellationToken token = this.TestToken;

            CertificateAuthority ca = await CertificateAuthority.CreateAsync(
                registrationId,
                rootCa,
                caCertScriptPath,
                token);

            IdCertificates idCert = await ca.GenerateIdentityCertificatesAsync(registrationId, token);

            // The trust bundle for this test isn't used. It can be any arbitrary existing certificate.
            string trustBundle = Path.Combine(
                caCertScriptPath,
                "certs",
                "azure-iot-test-only.intermediate-full-chain.cert.pem");

            await this.daemon.ConfigureAsync(
                config =>
                {
                    config.SetDpsX509(idScope, registrationId, idCert, trustBundle);
                    config.Update();
                    return Task.FromResult((
                        "with DPS X509 attestation for '{Identity}'",
                        new object[] { registrationId }));
                },
                token);

            await this.daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

            var agent = new EdgeAgent(registrationId, this.iotHub);
            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
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
