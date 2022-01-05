// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;

    [EndToEnd]
    public class ContentTrust : ManifestTrustSetupFixture
    {
        const string SignedImageModuleName = "signedImage";
        const string UnsignedImageModuleName = "unsignedImage";
        string signedImage = Context.Current.ContentTrustSignedImage.OrDefault();
        string unsignedImage = Context.Current.ContentTrustUnsignedImage.OrDefault();
        EdgeModule signedImageModule;
        DateTime startTime;

        async Task SetConfigToEdgeDaemon(Option<Dictionary<string, string>> contentTrustInputs, CancellationToken token)
        {
            // This is a temporary solution see ticket: 9288683
            if (!Context.Current.ISA95Tag)
            {
                TestCertificates testCerts;
                (testCerts, this.ca) = await TestCertificates.GenerateCertsAsync(this.device.Id, token);
                this.startTime = DateTime.Now;

                await this.ConfigureDaemonAsync(
                    config =>
                    {
                        testCerts.AddCertsToConfigForManifestTrust(config, Option.None<string>(), contentTrustInputs);

                        config.SetManualSasProvisioning(this.IotHub.Hostname, Context.Current.ParentHostname, this.device.Id, this.device.SharedAccessKey);

                        config.Update();

                        return Task.FromResult((
                            "with connection string for device '{Identity}'",
                            new object[] { this.device.Id }));
                    },
                    this.device,
                    this.startTime,
                    token);
            }
        }

        // Content trust is not supported as of now on ARM platforms
        [Category("contenttrust")]
        [Category("FlakyOnArm")]
        [Test]
        public async Task TestContentTrustDeployment()
        {
            // Create the input dictionary of the mapping of container registry name and its corresponding root CA
            var contentTrustInput = new Dictionary<string, string>();
            contentTrustInput.Add(Context.Current.ContentTrustRegistryName.OrDefault(), Context.Current.ContentTrustRootCaPath.OrDefault());

            // Edge Daemon is configured with a content trust root CA
            await this.SetConfigToEdgeDaemon(Option.Some(contentTrustInput), this.TestToken);

            // This is a temporary solution see ticket: 9288683
            if (!Context.Current.ISA95Tag)
            {
                EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                    builder =>
                    {
                        builder.AddModule(SignedImageModuleName, this.signedImage)
                            .WithEnvironment(new[] { ("MessageCount", "-1") });
                    },
                    this.TestToken,
                    Context.Current.NestedEdge,
                    null);
                this.signedImageModule = deployment.Modules[SignedImageModuleName];
                this.startTime = deployment.StartTime;
            }
            else
            {
                this.signedImageModule = new EdgeModule(SignedImageModuleName, this.runtime.DeviceId, this.IotHub);
                this.startTime = DateTime.Now;
            }

            await this.signedImageModule.WaitForEventsReceivedAsync(this.startTime, this.TestToken);

            // This is a temporary solution see ticket: 9288683
            if (!Context.Current.ISA95Tag)
            {
                bool isCancelled = false;
                CancellationTokenSource unsignedImageToken = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                try
                {
                    EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                        builder =>
                        {
                            builder.AddModule(UnsignedImageModuleName, this.unsignedImage)
                                .WithEnvironment(new[] { ("MessageCount", "-1") });
                        },
                        unsignedImageToken.Token,
                        Context.Current.NestedEdge,
                        null);
                }
                catch (TaskCanceledException)
                {
                    isCancelled = true;
                }

                Assert.IsTrue(isCancelled);
            }
        }
    }
}
