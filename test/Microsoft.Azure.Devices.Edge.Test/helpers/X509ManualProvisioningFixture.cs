// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using NUnit.Framework;

    public class X509ManualProvisioningFixture : ManualProvisioningFixture
    {
        protected EdgeRuntime runtime;

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

                        (X509Thumbprint thumbprint, IdCertificates certs) = await this.CreateIdentityCertAsync(
                            deviceId, token);

                        EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                            deviceId,
                            this.iotHub,
                            AuthenticationType.SelfSigned,
                            thumbprint,
                            token);

                        Context.Current.DeleteList.TryAdd(device.Id, device);

                        this.runtime = new EdgeRuntime(
                            device.Id,
                            Context.Current.EdgeAgentImage,
                            Context.Current.EdgeHubImage,
                            Context.Current.Proxy,
                            Context.Current.Registries,
                            Context.Current.OptimizeForPerformance,
                            this.iotHub);

                        await this.ConfigureDaemonAsync(
                            config =>
                            {
                                config.SetDeviceManualX509(
                                    device.HubHostname,
                                    device.Id,
                                    certs.CertificatePath,
                                    certs.KeyPath);
                                config.Update();
                                return Task.FromResult((
                                    "with x509 certificate for device '{Identity}'",
                                    new object[] { device.Id }));
                            },
                            device,
                            startTime,
                            token);
                    }
                },
                "Completed edge manual provisioning with self-signed certificate");
        }

        async Task<(X509Thumbprint, IdCertificates)> CreateIdentityCertAsync(string deviceId, CancellationToken token)
        {
            (string, string, string) rootCa =
            Context.Current.RootCaKeys.Expect(() => new InvalidOperationException("Missing root CA keys"));
            string caCertScriptPath = Context.Current.CaCertScriptPath.Expect(
                () => new InvalidOperationException("Missing CA cert script path"));
            string idScope = Context.Current.DpsIdScope.Expect(
                () => new InvalidOperationException("Missing DPS ID scope"));

            CertificateAuthority ca = await CertificateAuthority.CreateAsync(
                deviceId,
                rootCa,
                caCertScriptPath,
                token);

            var identityCerts = await ca.GenerateIdentityCertificatesAsync(deviceId, token);

            X509Certificate2 deviceCert = new X509Certificate2(identityCerts.CertificatePath);

            return (new X509Thumbprint()
            {
                PrimaryThumbprint = deviceCert.Thumbprint
            },
            identityCerts);
        }
    }
}
