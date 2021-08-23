// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
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
        // EdgeModule sensor;
        DateTime startTime;

        public async void SetConfigToEdgeDaemon(Option<string> rootCaPath, CancellationToken token)
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
        }

        public void SetLaunchSettingsWithRootCa(Option<string> defaultLaunchSettings, Option<string> rootCaPath)
        {
            if (defaultLaunchSettings.HasValue && rootCaPath.HasValue)
            {
                string defaultLaunchSettingsString = defaultLaunchSettings.OrDefault();
                Console.WriteLine(defaultLaunchSettingsString);
                JObject defaultJsonObject = JObject.Parse(defaultLaunchSettingsString);
                if (defaultJsonObject["profiles"]["ManifestSignerClient"]["environmentVariables"] != null)
                {
                    defaultJsonObject["profiles"]["ManifestSignerClient"]["environmentVariables"]["MANIFEST_TRUST_DEVICE_ROOT_CA_PATH"] = rootCaPath.OrDefault();
                }
            }
        }

        [Category("ManifestSigning")]
        [Test]
        public void TestIfSignedDeploymentIsSuccessful()
        {
            // this.SetConfigToEdgeDaemon(Context.Current.ManifestSigningGoodRootCaPath, this.TestToken);
            this.SetLaunchSettingsWithRootCa(Context.Current.ManifestSigningDefaultLaunchSettings, Context.Current.ManifestSigningGoodRootCaPath);
        }

        /*[Category("ManifestSigning")]
        [Test]
        public async Task TestIfSignedDeploymentIsSuccessful()
        {
            this.SetConfigToEdgeDaemon(Context.Current.ManifestSigningGoodRootCaPath, this.TestToken);

            this.SetLaunchSettingsWithRootCa(Context.Current.ManifestSigningDefaultLaunchSettings, Context.Current.ManifestSigningGoodRootCaPath);

            ManifestSettings inputManifestSettings = new ManifestSettings(Context.Current.ManifestSigningDeploymentPath, Context.Current.ManifestSigningSignedDeploymentPath, Context.Current.ManifestSigningGoodRootCaPath, Context.Current.ManifestSignerClientBinPath);

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(SensorName, this.sensorImage)
                        .WithEnvironment(new[] { ("MessageCount", "-1") });
                },
                this.TestToken,
                Context.Current.NestedEdge,
                inputManifestSettings);
        }*/

        /*[Category("ManifestSigning")]
        [Test]
        public async Task TestIfSignedDeploymentIsConfiguredWithBadRootCa()
        {
            this.SetConfigToEdgeDaemon(Context.Current.ManifestSigningBadRootCaPath, this.TestToken);

            this.SetLaunchSettingsWithRootCa(Context.Current.ManifestSigningDefaultLaunchSettings, Context.Current.ManifestSigningBadRootCaPath);

            ManifestSettings inputManifestSettings = new ManifestSettings(Context.Current.ManifestSigningDeploymentPath, Context.Current.ManifestSigningSignedDeploymentPath, Context.Current.ManifestSigningGoodRootCaPath, Context.Current.ManifestSignerClientBinPath);

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

            await this.sensor.WaitForEventsReceivedAsync(this.startTime, this.TestToken);

            await this.sensor.UpdateDesiredPropertiesAsync(
                new
                {
                    properties = new
                    {
                        desired = new
                        {
                            SendData = true,
                            SendInterval = 10
                        }
                    }
                },
                this.TestToken);

            await this.sensor.WaitForReportedPropertyUpdatesAsync(
                new
                {
                    properties = new
                    {
                        reported = new
                        {
                            SendData = true,
                            SendInterval = 10
                        }
                    }
                },
                this.TestToken);
        }

        [Category("ManifestSigning")]
        [Test]
        public async Task TestIfSignedDeploymentIsConfiguredWithNoRootCa()
        {
            this.SetConfigToEdgeDaemon(Option.None<string>(), this.TestToken);
            this.SetLaunchSettingsWithRootCa(Context.Current.ManifestSigningDefaultLaunchSettings, Context.Current.ManifestSigningGoodRootCaPath);

            ManifestSettings inputManifestSettings = new ManifestSettings(Context.Current.ManifestSigningDeploymentPath, Context.Current.ManifestSigningSignedDeploymentPath, Context.Current.ManifestSigningGoodRootCaPath, Context.Current.ManifestSignerClientBinPath);

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

            await this.sensor.WaitForEventsReceivedAsync(this.startTime, this.TestToken);

            await this.sensor.UpdateDesiredPropertiesAsync(
                new
                {
                    properties = new
                    {
                        desired = new
                        {
                            SendData = true,
                            SendInterval = 10
                        }
                    }
                },
                this.TestToken);

            await this.sensor.WaitForReportedPropertyUpdatesAsync(
                new
                {
                    properties = new
                    {
                        reported = new
                        {
                            SendData = true,
                            SendInterval = 10
                        }
                    }
                },
                this.TestToken);
        }
        */
    }
}
