// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [EndToEnd]
    public class ManifestSigning : ManifestSigningSetupFixture
    {
        const string SensorName = "tempSensor";
        const string DefaultSensorImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0";
        string sensorImage = Context.Current.TempSensorImage.GetOrElse(DefaultSensorImage);
        EdgeModule sensor;
        DateTime startTime;

        public async Task SetConfigToEdgeDaemon(Option<string> rootCaPath, CancellationToken token)
        {
            if (Context.Current.EnableManifestSigning)
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
                            testCerts.AddCertsToConfigForManifestSigning(config, rootCaPath);

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
            else
            {
                if (!Context.Current.ISA95Tag)
                {
                    TestCertificates testCerts;
                    (testCerts, this.ca) = await TestCertificates.GenerateCertsAsync(this.device.Id, token);
                    this.startTime = DateTime.Now;
                    await this.ConfigureDaemonAsync(
                        config =>
                        {
                            testCerts.AddCertsToConfig(config);

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
        }

        public void SetLaunchSettingsWithRootCa(Option<string> defaultLaunchSettings, Option<string> rootCaPath)
        {
            // get the default launch settings and update with the root CA required for the test
            if (defaultLaunchSettings.HasValue && rootCaPath.HasValue)
            {
                string defaultLaunchSettingsString = defaultLaunchSettings.OrDefault();
                Console.WriteLine($"Default Launch Settings:  {defaultLaunchSettingsString}");
                JObject defaultJsonObject = JObject.Parse(defaultLaunchSettingsString);
                if (defaultJsonObject["profiles"]["ManifestSignerClient"]["environmentVariables"] != null)
                {
                    Console.WriteLine("Updated Root CA location");
                    defaultJsonObject["profiles"]["ManifestSignerClient"]["environmentVariables"]["MANIFEST_TRUST_DEVICE_ROOT_CA_PATH"] = rootCaPath.OrDefault();
                }

                // Wrtie the modified launch settings to the file
                File.WriteAllText(Context.Current.ManifestSigningLaunchSettingsPath.OrDefault(), defaultJsonObject.ToString());
                string newLauchSettingsContents = File.ReadAllText(Context.Current.ManifestSigningLaunchSettingsPath.OrDefault());
                Console.WriteLine($"new Launch Settings from SetLaunchSettingsWithRootCa: {newLauchSettingsContents} ");
            }
        }

        [Category("ManifestSigning")]
        [Test]
        public async Task TestIfSignedDeploymentIsSuccessful()
        {
            this.SetLaunchSettingsWithRootCa(Context.Current.ManifestSigningDefaultLaunchSettings, Context.Current.ManifestSigningGoodRootCaPath);

            await this.SetConfigToEdgeDaemon(Context.Current.ManifestSigningGoodRootCaPath, this.TestToken);
            ManifestSettings inputManifestSettings = new ManifestSettings(Context.Current.ManifestSigningDeploymentPath, Context.Current.ManifestSigningSignedDeploymentPath, Context.Current.ManifestSigningGoodRootCaPath, Context.Current.ManifestSignerClientDirectory, Context.Current.ManifestSignerClientProjectPath);

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

        /*[Category("ManifestSigning")]
        [Test]
        public async Task TestIfSignedDeploymentIsConfiguredWithBadRootCa()
        {
            this.SetLaunchSettingsWithRootCa(Context.Current.ManifestSigningDefaultLaunchSettings, Context.Current.ManifestSigningBadRootCaPath);

            await this.SetConfigToEdgeDaemon(Context.Current.ManifestSigningBadRootCaPath, this.TestToken);
            ManifestSettings inputManifestSettings = new ManifestSettings(Context.Current.ManifestSigningDeploymentPath, Context.Current.ManifestSigningSignedDeploymentPath, Context.Current.ManifestSigningBadRootCaPath, Context.Current.ManifestSignerClientDirectory, Context.Current.ManifestSignerClientProjectPath);

            try
            {
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
            catch (TaskCanceledException)
            {
                Assert.AreNotEqual();
            }
        }

        [Category("ManifestSigning")]
        [Test]
        public async Task TestIfSignedDeploymentIsConfiguredWithNoRootCa()
        {
            this.SetLaunchSettingsWithRootCa(Context.Current.ManifestSigningDefaultLaunchSettings, Option.None<string>());

            await this.SetConfigToEdgeDaemon(Context.Current.ManifestSigningBadRootCaPath, this.TestToken);
            ManifestSettings inputManifestSettings = new ManifestSettings(Context.Current.ManifestSigningDeploymentPath, Context.Current.ManifestSigningSignedDeploymentPath, Option.None<string>(), Context.Current.ManifestSignerClientDirectory, Context.Current.ManifestSignerClientProjectPath);

            try
            {
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
            catch (TaskCanceledException )
            {
            }
        }
        */
    }
}