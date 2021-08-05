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
    using NUnit.Framework;

    [EndToEnd]
    public class ManifestSigning : ManifestSigningSetupFixture
    {
        const string SensorName = "tempSensor";
        const string DefaultSensorImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0";

        [Test]
        public async Task TestIfSignedDeploymentIsSuccessful()
        {
            string sensorImage = Context.Current.TempSensorImage.GetOrElse(DefaultSensorImage);
            CancellationToken token = this.TestToken;

            EdgeModule sensor;
            DateTime startTime;

            Option<ManifestSettings> inputManifestSettings = Option.Some(new ManifestSettings(Context.Current.ManifestSigningDeploymentDir, Context.Current.ManifestSigningGoodRootCaPath, Context.Current.ManifestSigningLaunchSettingsPath));

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(SensorName, sensorImage)
                        .WithEnvironment(new[] { ("MessageCount", "-1") });
                },
                token,
                Context.Current.NestedEdge,
                inputManifestSettings);

            sensor = deployment.Modules[SensorName];
            startTime = deployment.StartTime;

            await sensor.WaitForEventsReceivedAsync(startTime, token);

            await sensor.UpdateDesiredPropertiesAsync(
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
                token);
            await sensor.WaitForReportedPropertyUpdatesAsync(
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
                token);
        }

        [Test]
        public async Task TestIfSignedDeploymentIsFailedWithNoRootCA()
        {
            string sensorImage = Context.Current.TempSensorImage.GetOrElse(DefaultSensorImage);
            CancellationToken token = this.TestToken;

            EdgeModule sensor;
            DateTime startTime;

            Option<ManifestSettings> inputManifestSettings = Option.Some(new ManifestSettings(Context.Current.ManifestSigningDeploymentDir, Option.None<string>(), Context.Current.ManifestSigningLaunchSettingsPath));

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(SensorName, sensorImage)
                        .WithEnvironment(new[] { ("MessageCount", "-1") });
                },
                token,
                Context.Current.NestedEdge,
                inputManifestSettings);

            sensor = deployment.Modules[SensorName];
            startTime = deployment.StartTime;

            await sensor.WaitForEventsReceivedAsync(startTime, token);

            await sensor.UpdateDesiredPropertiesAsync(
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
                token);
            await sensor.WaitForReportedPropertyUpdatesAsync(
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
                token);
        }
    }
}
