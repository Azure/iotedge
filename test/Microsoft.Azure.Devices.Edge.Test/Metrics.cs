// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Newtonsoft.Json;
    using NUnit.Framework;

    using ConfigModuleName = Microsoft.Azure.Devices.Edge.Test.Common.Config.ModuleName;

    [EndToEnd]
    public class Metrics : SasManualProvisioningFixture
    {
        public const string ModuleName = "metricsValidator";
        const string EdgeAgentBaseImage = "mcr.microsoft.com/azureiotedge-agent:1.0";

        [Test]
        public async Task ValidateMetrics()
        {
            CancellationToken token = this.TestToken;
            await this.Deploy(token);

            var result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ModuleName, new CloudToDeviceMethod("ValidateMetrics", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)), token);
            Assert.AreEqual(result.Status, (int)HttpStatusCode.OK);

            string body = result.GetPayloadAsJson();
            Report report = JsonConvert.DeserializeObject<Report>(body, new JsonSerializerSettings() { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });
            Assert.Zero(report.Failed, body);
        }

        class Report
        {
            public int Succeeded { get; set; }
            public int Failed { get; set; }
        }

        async Task Deploy(CancellationToken token)
        {
            // First deploy everything needed for this test, including a temporary image that will be removed later to bump the "stopped" metric
            string metricsValidatorImage = Context.Current.MetricsValidatorImage.Expect(() => new InvalidOperationException("Missing Metrics Validator image"));
            await this.runtime.DeployConfigurationAsync(
                builder =>
                    {
                        builder.AddTemporaryModule();
                        builder.AddMetricsValidatorConfig(metricsValidatorImage);
                    }, token);

            // Next remove the temporary image from the deployment
            await this.runtime.DeployConfigurationAsync(
                builder => { builder.AddMetricsValidatorConfig(metricsValidatorImage); },
                token,
                stageSystemModules: false);
        }
    }

    static class EdgeConfigBuilderEx
    {
        public static void AddTemporaryModule(this EdgeConfigBuilder builder)
        {
            const string Name = "stopMe";
            const string Image = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0";
            builder.AddModule(Name, Image).WithEnvironment(new[] { ("MessageCount", "0") });
        }

        public static void AddMetricsValidatorConfig(this EdgeConfigBuilder builder, string image)
        {
            builder.AddModule(Metrics.ModuleName, image);

            var edgeHub = builder.GetModule(ConfigModuleName.EdgeHub)
                .WithDesiredProperties(new Dictionary<string, object>
                {
                    {
                        "routes", new
                        {
                            All = "FROM /messages/* INTO $upstream",
                            QueueLengthTest = $"FROM /messages/modules/{Metrics.ModuleName}/outputs/ToSelf INTO BrokeredEndpoint(\"/modules/{Metrics.ModuleName}/inputs/FromSelf\")"
                        }
                    }
                });

            builder.GetModule("$edgeAgent").WithEnvironment(("PerformanceMetricsUpdateFrequency", "00:00:20"));
        }
    }
}
