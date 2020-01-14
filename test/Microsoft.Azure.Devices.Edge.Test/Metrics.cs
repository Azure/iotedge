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
        string moduleName = "MetricsValidator";
        string imageName = " edgebuilds.azurecr.io/microsoft/azureiotedge-metrics-validator:20200111.6-linux-amd64";
        /* string imageName = "lefitchereg1.azurecr.io/metrics_validator_test:0.0.1-amd64"; */

        [Test]
        public async Task ValidateMetrics()
        {
            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(this.BaseConfig, token);

            // System resource metrics wait 1 minute to start collecting.
            await Task.Delay(TimeSpan.FromMinutes(1.1), token);

            var result = await this.iotHub.InvokeMethodAsync(Context.Current.DeviceId, this.moduleName, new CloudToDeviceMethod("ValidateMetrics"), CancellationToken.None);
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

        void BaseConfig(EdgeConfigBuilder builder)
        {
            builder.AddModule(this.moduleName, this.imageName);
            builder.GetModule("$edgeHub")
                .WithEnvironment(new[]
                {
                    ("experimentalfeatures__enabled", "true"),
                    ("experimentalfeatures__enableMetrics", "true")
                });
            builder.GetModule("$edgeAgent")
                .WithEnvironment(new[]
                {
                    ("experimentalfeatures__enabled", "true"),
                    ("experimentalfeatures__enableMetrics", "true")
                });
        }
    }
}
