// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [EndToEnd]
    public class ManifestSigning : ManifestTrustSetupFixture
    {
        const string SensorName = "tempSensor";
        const string DefaultSensorImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0";
        string sensorImage = Context.Current.TempSensorImage.GetOrElse(DefaultSensorImage);
        EdgeModule sensor;
        DateTime startTime;

        async Task SetConfigToEdgeDaemon(Option<string> manifestTrustBundle, CancellationToken token)
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
                        testCerts.AddCertsToConfigForManifestTrust(config, manifestTrustBundle, Option.None<Dictionary<string, string>>());

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

        void SetLaunchSettingsWithRootCa(Option<string> defaultLaunchSettings, Option<string> rootCaPath)
        {
            // get the default launch settings and update with the root CA required for the test
            if (defaultLaunchSettings.HasValue && rootCaPath.HasValue)
            {
                string defaultLaunchSettingsString = defaultLaunchSettings.OrDefault();
                JObject defaultJsonObject = JObject.Parse(defaultLaunchSettingsString);
                if (defaultJsonObject["profiles"]["ManifestSignerClient"]["environmentVariables"] != null)
                {
                    defaultJsonObject["profiles"]["ManifestSignerClient"]["environmentVariables"]["MANIFEST_TRUST_DEVICE_ROOT_CA_PATH"] = rootCaPath.OrDefault();
                }

                // Wrtie the modified launch settings to the file
                File.WriteAllText(Context.Current.ManifestSigningLaunchSettingsPath.OrDefault(), defaultJsonObject.ToString());
                string newLauchSettingsContents = File.ReadAllText(Context.Current.ManifestSigningLaunchSettingsPath.OrDefault());
            }
        }

        // NOTE: temporarily marked as fully flaky due to downstream issues.
        [Test]
        [Category("Flaky")]
        // [Category("FlakyOnArm")]
        // [Category("FlakyOnRelease")]
        public async Task TestIfSignedDeploymentIsSuccessful()
        {
            // Edge Daemon is configured with a good root CA and manifest is signed.
            this.SetLaunchSettingsWithRootCa(Context.Current.ManifestSigningDefaultLaunchSettings, Context.Current.ManifestSigningGoodRootCaPath);
            ManifestSettings inputManifestSettings = new ManifestSettings(Context.Current.ManifestSigningDeploymentPath, Context.Current.ManifestSigningSignedDeploymentPath, Context.Current.ManifestSigningGoodRootCaPath, Context.Current.ManifestSignerClientDirectory, Context.Current.ManifestSignerClientProjectPath);

            await this.SetConfigToEdgeDaemon(Context.Current.ManifestSigningGoodRootCaPath, this.TestToken);

            // This is a temporary solution see ticket: 9288683
            if (!Context.Current.ISA95Tag)
            {
                EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                    builder =>
                    {
                        builder.AddModule(SensorName, this.sensorImage)
                            .WithEnvironment(new[] { ("MessageCount", "-1") });
                    },
                    this.TestToken,
                    Context.Current.NestedEdge,
                    inputManifestSettings);
                this.sensor = deployment.Modules[SensorName];
                this.startTime = deployment.StartTime;
            }
            else
            {
                this.sensor = new EdgeModule(SensorName, this.runtime.DeviceId, this.IotHub);
                this.startTime = DateTime.Now;
            }

            await this.sensor.WaitForEventsReceivedAsync(this.startTime, this.TestToken);
        }

        [Category("Flaky")]
        [Category("FlakyOnArm")]
        [Test]
        public async Task TestIfSignedDeploymentIsConfiguredWithBadRootCa()
        {
            // Edge Daemon is configured with a bad root CA but manifest is signed.
            this.SetLaunchSettingsWithRootCa(Context.Current.ManifestSigningDefaultLaunchSettings, Context.Current.ManifestSigningGoodRootCaPath);
            ManifestSettings inputManifestSettings = new ManifestSettings(Context.Current.ManifestSigningDeploymentPath, Context.Current.ManifestSigningSignedDeploymentPath, Context.Current.ManifestSigningGoodRootCaPath, Context.Current.ManifestSignerClientDirectory, Context.Current.ManifestSignerClientProjectPath);

            await this.SetConfigToEdgeDaemon(Context.Current.ManifestSigningBadRootCaPath, this.TestToken);

            // Set a faster time out as its expected to fail.
            CancellationTokenSource manifestSigningCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            bool isThereException = false;

            // This is a temporary solution see ticket: 9288683
            if (!Context.Current.ISA95Tag)
            {
                try
                {
                    EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                        builder =>
                        {
                            builder.AddModule(SensorName, this.sensorImage)
                                .WithEnvironment(new[] { ("MessageCount", "-1") });
                        },
                        manifestSigningCts.Token,
                        Context.Current.NestedEdge,
                        inputManifestSettings);
                }
                catch (TaskCanceledException)
                {
                    isThereException = true;
                }

                CancellationTokenSource getTwinTimer = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                Twin twin = await this.IotHub.GetTwinAsync(this.runtime.DeviceId, Option.Some("$edgeAgent"), getTwinTimer.Token);

                Assert.IsTrue(isThereException);
                // EdgeAgent will reject the new deployment, so the reported and desired versions will not match
                Assert.AreNotEqual(twin.Properties.Desired.Version, twin.Properties.Reported.GetLastUpdatedVersion());
            }
        }

        [Test]
        [Category("FlakyOnArm")]
        public async Task TestIfSignedDeploymentIsConfiguredWithNoRootCa()
        {
            // Edge Daemon is not configured but manifest is signed.
            this.SetLaunchSettingsWithRootCa(Context.Current.ManifestSigningDefaultLaunchSettings, Context.Current.ManifestSigningGoodRootCaPath);
            ManifestSettings inputManifestSettings = new ManifestSettings(Context.Current.ManifestSigningDeploymentPath, Context.Current.ManifestSigningSignedDeploymentPath, Context.Current.ManifestSigningGoodRootCaPath, Context.Current.ManifestSignerClientDirectory, Context.Current.ManifestSignerClientProjectPath);

            await this.SetConfigToEdgeDaemon(Option.None<string>(), this.TestToken);

            // Set a faster time out as its expected to fail.
            CancellationTokenSource manifestSigningCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            bool isThereException = false;

            // This is a temporary solution see ticket: 9288683
            if (!Context.Current.ISA95Tag)
            {
                try
                {
                    EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                        builder =>
                        {
                            builder.AddModule(SensorName, this.sensorImage)
                                .WithEnvironment(new[] { ("MessageCount", "-1") });
                        },
                        manifestSigningCts.Token,
                        Context.Current.NestedEdge,
                        inputManifestSettings);
                }
                catch (TaskCanceledException)
                {
                    isThereException = true;
                }

                CancellationTokenSource getTwinTimer = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                Twin twin = await this.IotHub.GetTwinAsync(this.runtime.DeviceId, Option.Some("$edgeAgent"), getTwinTimer.Token);

                Assert.IsTrue(isThereException);
                // EdgeAgent will reject the new deployment, so the reported and desired versions will not match
                Assert.AreNotEqual(twin.Properties.Desired.Version, twin.Properties.Reported.GetLastUpdatedVersion());
            }
        }
    }
}
