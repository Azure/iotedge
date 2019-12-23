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
        public X509ManualProvisioningFixture()
            : base("-x509")
        {
        }

        public X509Thumbprint Thumbprint { get; set; }
        public EdgeDevice Device { get; set; }

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

                        IdCertificates certs;
                        (this.Thumbprint, certs) = await this.CreateDeviceIdCertAsync(Context.Current.DeviceId + "-x509", token);

                        EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                            Context.Current.DeviceId + "-x509",
                            this.iotHub,
                            AuthenticationType.SelfSigned,
                            this.Thumbprint,
                            token);

                        Context.Current.DeleteList.TryAdd(device.Id, device);

                        this.Device = device;

                        await this.ManuallyProvisionEdgeX509Async(device, certs.CertificatePath, certs.KeyPath, startTime, token);
                    }
                },
                "Completed edge manual provisioning with self-signed certificate");
        }

        private async Task<(X509Thumbprint, IdCertificates)> CreateDeviceIdCertAsync(string deviceId, CancellationToken token)
        {
            (string, string, string) rootCa =
            Context.Current.RootCaKeys.Expect(() => new InvalidOperationException("Missing root CA keys"));
            string caCertScriptPath =
                        Context.Current.CaCertScriptPath.Expect(() => new InvalidOperationException("Missing CA cert script path"));
            string idScope = Context.Current.DpsIdScope.Expect(() => new InvalidOperationException("Missing DPS ID scope"));

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
