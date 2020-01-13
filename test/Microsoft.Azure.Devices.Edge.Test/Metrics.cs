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
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Serilog;

    public class Metrics : SasManualProvisioningFixture
    {
        string moduleName = "MetricsValidator";
        string imageName = " edgebuilds.azurecr.io/microsoft/azureiotedge-metrics-validator:20200111.6-linux-amd64";
        /* string imageName = "lefitchereg1.azurecr.io/metrics_validator_test:0.0.1-amd64"; */

        [Test]
        public async Task ValidateMetrics()
        {
            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(this.moduleName, this.imageName);
                    builder.GetModule("$edgeHub")
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["routes"] = new
                            {
                                TempFilterToCloud = "FROM /messages/modules/" + this.moduleName + "/* INTO $upstream",
                            },
                        })
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
                },
                token);

            Log.Information("Calling direct method");
            var result = await this.iotHub.InvokeMethodAsync(Context.Current.DeviceId, this.moduleName, new CloudToDeviceMethod("ValidateMetrics"), CancellationToken.None);
            Log.Information($"Got result {result.GetPayloadAsJson()}");
            Assert.AreEqual(result.Status, (int)HttpStatusCode.OK);

            string body = result.GetPayloadAsJson();
            Console.WriteLine(body);
            Report report = JsonConvert.DeserializeObject<Report>(body);
            Log.Information($"Parsed body: {JsonConvert.SerializeObject(report)}");

            Assert.Zero(report.Failed, body);
        }

        class Report
        {
            public int Succeeded { get; }
            public int Failed { get; }
        }
    }
}
