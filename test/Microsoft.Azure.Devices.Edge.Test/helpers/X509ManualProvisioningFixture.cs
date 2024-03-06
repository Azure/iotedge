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
                        string certsPath = this.daemon.GetCertificatesPath();

                        var idCerts = await TestCertificates.GenerateIdentityCertificatesAsync(
                            deviceId,
                            certsPath,
                            token);
                        var deviceCert = idCerts.Certificate;
                        var thumbprint = new X509Thumbprint()
                        {
                            PrimaryThumbprint = deviceCert.Thumbprint,
                            SecondaryThumbprint = deviceCert.Thumbprint
                        };

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

                        (var certs, this.ca) = await TestCertificates.GenerateEdgeCaCertsAsync(
                            device.Id,
                            certsPath,
                            token);

                        await this.ConfigureDaemonAsync(
                            async config =>
                            {
                                config.SetCertificates(certs);
                                config.SetDeviceManualX509(
                                    this.IotHub.Hostname,
                                    Context.Current.ParentHostname,
                                    device.Id,
                                    idCerts.CertificatePath,
                                    idCerts.KeyPath);
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
    }
}
