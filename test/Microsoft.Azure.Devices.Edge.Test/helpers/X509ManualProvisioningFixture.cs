// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;

    public class X509ManualProvisioningFixture : ManualProvisioningFixture
    {
        protected static EdgeRuntime runtime;
        protected static EdgeDevice device;

        [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
        public static async Task X509ProvisionEdgeAsync(TestContext msTestContext)
        {
            await Profiler.Run(
                async () =>
                {
                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        CancellationToken token = cts.Token;
                        DateTime startTime = DateTime.Now;
                        string deviceId = DeviceId.Current.Generate();
                        string certsPath = daemon.GetCertificatesPath();

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
                            GetNestedEdgeConfig(IotHub),
                            IotHub,
                            AuthenticationType.SelfSigned,
                            thumbprint,
                            token);

                        Context.Current.DeleteList.TryAdd(device.Id, device);

                        runtime = new EdgeRuntime(
                            device.Id,
                            Context.Current.EdgeAgentImage,
                            Context.Current.EdgeHubImage,
                            Context.Current.EdgeProxy,
                            Context.Current.Registries,
                            Context.Current.OptimizeForPerformance,
                            IotHub);

                        (var certs, ca) = await TestCertificates.GenerateEdgeCaCertsAsync(
                            device.Id,
                            certsPath,
                            token);

                        await ConfigureDaemonAsync(
                            async config =>
                            {
                                config.SetCertificates(certs);
                                config.SetDeviceManualX509(
                                    IotHub.Hostname,
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
