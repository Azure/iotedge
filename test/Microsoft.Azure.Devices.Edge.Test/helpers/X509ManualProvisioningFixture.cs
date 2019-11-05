// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;

    public class X509ManualProvisioningFixture : ManualProvisioningFixture
    {
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

                        this.Thumbprint = this.CreateSelfSignedCertificateThumbprint();

                        EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                        Context.Current.DeviceId + "-x509",
                        this.iotHub,
                        AuthenticationType.SelfSigned,
                        this.Thumbprint,
                        token);

                        Context.Current.DeleteList.TryAdd(device.Id, device);

                        this.Device = device;

                        await this.ManuallyProvisionEdgeAsync(device, startTime, token);
                    }
                },
                "Completed edge manual provisioning with self-signed certificate");
        }

        private X509Thumbprint CreateSelfSignedCertificateThumbprint()
        {
            return new X509Thumbprint()
            {
                PrimaryThumbprint = "9991572f0a02bdc7c89fc032b95d79aca18ef7a3",
                SecondaryThumbprint = "9991572f0a02bdc7c89fc032b95d79aca18ef7a4"
            };
        }
    }
}
