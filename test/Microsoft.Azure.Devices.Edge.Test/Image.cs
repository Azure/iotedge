// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;
    using Serilog;

    [EndToEnd]
    public class Image : SasManualProvisioningFixture
    {
        const string SensorName = "tempSensor";
        const string DefaultSensorImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.4";

        [Test]
        [Category("CentOsSafe")]
        public async Task ImageGarbageCollection()
        {
            CancellationToken token = this.TestToken;

            // Create initial deployment with simulated temperature sensor
            string sensorImage = Context.Current.TempSensorImage.GetOrElse(DefaultSensorImage);
            EdgeDeployment deployment1 = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(SensorName, sensorImage);
                },
                this.cli,
                token,
                Context.Current.NestedEdge);
            EdgeModule sensor = deployment1.Modules[SensorName];
            await sensor.WaitForStatusAsync(EdgeModuleStatus.Running, this.cli, token);

            // Create second deployment without simulated temperature sensor
            EdgeDeployment deployment2 = await this.runtime.DeployConfigurationAsync(
                this.cli,
                token,
                Context.Current.NestedEdge);

            // Configure image garbage collection to happen in 2 minutes
            await this.daemon.ConfigureAsync(
                async config =>
                {
                    config.SetImageGarbageCollection(2);
                    await config.UpdateAsync(token);
                    return ("with non-default image garbage collection settings.", new object[] { });
                },
                token,
                true);

            // Loop, listing docker images until sensorImage is pruned
            await this.WaitForImageGarbageCollection(sensorImage, token);
        }

        public Task WaitForImageGarbageCollection(string image, CancellationToken token) => Profiler.Run(
            async () =>
            {
                await Retry.Do(
                    async () =>
                    {
                        string args = $"image ls -q --filter=reference={image}";
                        Log.Verbose($"docker {args}");
                        string[] output = await Process.RunAsync("docker", args, token);
                        return output;
                    },
                    output => output.Length == 0, // wait until 'docker images' output no longer includes sensor image
                    f => { return true; },
                    TimeSpan.FromSeconds(30),
                    token);
            },
            "Garbage collection completed for image '{Image}'",
            image);
    }
}
