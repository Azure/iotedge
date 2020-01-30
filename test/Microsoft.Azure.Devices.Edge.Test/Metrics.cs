// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class Metrics : SasManualProvisioningFixture
    {
        const string ModuleName = "MetricsValidator";
        const string EdgeAgentBaseImage = "mcr.microsoft.com/azureiotedge-agent:1.0";

        [Test]
        public async Task ValidateMetrics()
        {
            CancellationToken token = this.TestToken;
            await this.Deploy(token);

            // System resource metrics take 1 minute to start. Wait before testing
            await Task.Delay(TimeSpan.FromMinutes(1.1));

            var result = await this.iotHub.InvokeMethodAsync(Context.Current.DeviceId, ModuleName, new CloudToDeviceMethod("ValidateMetrics"), CancellationToken.None);
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
            // First deploy different agent image. This will force agent to update environment variables
            await this.runtime.DeployConfigurationAsync(builder => builder.GetModule("$edgeAgent").WithSettings(("image", EdgeAgentBaseImage)), token);

            string metricsValidatorImage = Context.Current.MetricsValidatorImage.Expect(() => new InvalidOperationException("Missing Metrics Validator image"));
            await this.runtime.DeployConfigurationAsync(
                builder =>
                    {
                        builder.AddModule(ModuleName, metricsValidatorImage);
                        builder.GetModule("$edgeHub")
                            .WithEnvironment(("experimentalfeatures__enabled", "true"), ("experimentalfeatures__enableMetrics", "true"))
                            .WithDesiredProperties(new Dictionary<string, object> { { "routes", new { All = "FROM /messages/* INTO $upstream" } } });
                        builder.GetModule("$edgeAgent")
                            .WithEnvironment(("experimentalfeatures__enabled", "true"), ("experimentalfeatures__enableMetrics", "true"));
                    }, token);
        }
    }
}
