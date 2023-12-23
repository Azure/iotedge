// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using NUnit.Framework;

    public class X509ManualProvisioningFixture : ManualProvisioningFixture
    {
        protected EdgeRuntime runtime;
        protected EdgeDevice device;

        [OneTimeSetUp]
        public async Task X509ProvisionEdgeAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        CancellationToken token = cts.Token;
                        DateTime startTime = DateTime.Now;
                        string deviceId = DeviceId.Current.Generate();

                        (X509Thumbprint thumbprint, string certPath, string keyPath) = await this.CreateIdentityCertAsync(
                            deviceId, token);

                        EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                            deviceId,
                            this.GetNestedEdgeConfig(this.IotHub),
                            this.IotHub,
                            AuthenticationType.SelfSigned,
                            thumbprint,
                            token);

                        Context.Current.DeleteList.TryAdd(device.Id, device);

                        this.runtime = new EdgeRuntime(
                            device.Id,
                            Context.Current.EdgeAgentImage,
                            Context.Current.EdgeHubImage,
                            Context.Current.EdgeProxy,
                            Context.Current.Registries,
                            Context.Current.OptimizeForPerformance,
                            this.IotHub);

                        TestCertificates testCerts;
                        (testCerts, this.ca) = await TestCertificates.GenerateCertsAsync(device.Id, token);

                        await this.ConfigureDaemonAsync(
                            async config =>
                            {
                                config.SetCertificates(testCerts.CaCertificates);
                                config.SetDeviceManualX509(
                                    this.IotHub.Hostname,
                                    Context.Current.ParentHostname,
                                    device.Id,
                                    certPath,
                                    keyPath);
                                await config.UpdateAsync(token);
                                return ("with x509 certificate for device '{Identity}'", new object[] { device.Id });
                            },
                            device,
                            startTime,
                            token);
                    }
                },
                "Completed edge manual provisioning with self-signed certificate");
        }

        async Task<(X509Thumbprint, string, string)> CreateIdentityCertAsync(string deviceId, CancellationToken token)
        {
            (string, string, string) rootCa =
            Context.Current.RootCaKeys.Expect(() => new InvalidOperationException("Missing DPS ID scope (check rootCaPrivateKeyPath in context.json)"));
            string caCertScriptPath = Context.Current.CaCertScriptPath.Expect(
                () => new InvalidOperationException("Missing CA cert script path (check caCertScriptPath in context.json)"));
            string idScope = Context.Current.DpsIdScope.Expect(
                () => new InvalidOperationException("Missing DPS ID scope(check dpsIdScope in context.json)"));
            string destPath = Path.Combine(FixedPaths.E2E_TEST_DIR, deviceId);

            CertificateAuthority ca = await CertificateAuthority.CreateAsync(
                deviceId,
                rootCa,
                caCertScriptPath,
                token);

            var identityCerts = await ca.GenerateIdentityCertificatesAsync(deviceId, destPath, token);
            X509Certificate2 deviceCert = new X509Certificate2(identityCerts.CertificatePath);

            return (
                new X509Thumbprint()
                {
                    PrimaryThumbprint = deviceCert.Thumbprint,
                    SecondaryThumbprint = deviceCert.Thumbprint
                },
                identityCerts.CertificatePath,
                identityCerts.KeyPath);
        }
    }
}
