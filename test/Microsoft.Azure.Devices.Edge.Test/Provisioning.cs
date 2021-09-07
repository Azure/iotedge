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

            CertificateAuthority ca = await CertificateAuthority.CreateAsync(
                registrationId,
                rootCa,
                caCertScriptPath,
                token);

            IdCertificates idCert = await ca.GenerateIdentityCertificatesAsync(registrationId, token);

            // The trust bundle for this test isn't used. It can be any arbitrary existing certificate.
            string trustBundle = Path.Combine(caCertScriptPath, FixedPaths.DeviceCaCert.TrustCert);

            // Generated credentials need to be copied out of the script path because future runs
            // of the script will overwrite them.
            string path = Path.Combine(FixedPaths.E2E_TEST_DIR, registrationId);
            string certPath = Path.Combine(path, "device_id_cert.pem");
            string keyPath = Path.Combine(path, "device_id_cert_key.pem");
            string trustBundlePath = Path.Combine(path, "trust_bundle.pem");

            Directory.CreateDirectory(path);
            File.Copy(idCert.CertificatePath, certPath);
            OsPlatform.Current.SetOwner(certPath, "aziotcs", "644");
            File.Copy(idCert.KeyPath, keyPath);
            OsPlatform.Current.SetOwner(keyPath, "aziotks", "600");
            File.Copy(trustBundle, trustBundlePath);
            OsPlatform.Current.SetOwner(trustBundlePath, "aziotcs", "644");

            await this.daemon.ConfigureAsync(
                config =>
                {
                    config.SetDpsX509(idScope, registrationId, certPath, keyPath, trustBundlePath);
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
