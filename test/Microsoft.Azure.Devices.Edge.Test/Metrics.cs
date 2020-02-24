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
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Newtonsoft.Json;
    using NUnit.Framework;

    using ConfigModuleName = Microsoft.Azure.Devices.Edge.Test.Common.Config.ModuleName;

    [EndToEnd]
    public class Metrics : SasManualProvisioningFixture
    {
        const string ModuleName = "MetricsValidator";
        const string EdgeAgentBaseImage = "mcr.microsoft.com/azureiotedge-agent:1.0";

        [Test]
        public async Task ValidateMetrics()
        {
            CancellationToken token = this.TestToken;
            await this.Deploy(token);

            var result = await this.iotHub.InvokeMethodAsync(Context.Current.DeviceId, ModuleName, new CloudToDeviceMethod("ValidateMetrics", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)), token, false);
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
            await this.runtime.DeployConfigurationAsync(builder => builder.GetModule(ConfigModuleName.EdgeAgent).WithSettings(("image", EdgeAgentBaseImage)), token);

            string metricsValidatorImage = Context.Current.MetricsValidatorImage.Expect(() => new InvalidOperationException("Missing Metrics Validator image"));
            await this.runtime.DeployConfigurationAsync(
                builder =>
                    {
                        builder.AddModule(ModuleName, metricsValidatorImage);

                        var edgeHub = builder.GetModule(ConfigModuleName.EdgeHub)
                            .WithEnvironment(("experimentalfeatures__enabled", "true"), ("experimentalfeatures__enableMetrics", "true"))
                            .WithDesiredProperties(new Dictionary<string, object>
                            {
                                {
                                    "routes", new
                                    {
                                        All = "FROM /messages/* INTO $upstream",
                                        QueueLengthTest = "FROM /messages/modules/MetricsValidator/outputs/ToSelf INTO BrokeredEndpoint(\"/modules/MetricsValidator/inputs/FromSelf\")"
                                    }
                                }
                            });
                        if (OsPlatform.IsWindows())
                        {
                            // Note: This overwrites the default port mapping. This if fine for this test.
                            edgeHub.WithSettings(("createOptions", "{\"User\":\"ContainerAdministrator\"}"));
                        }

                        builder.GetModule("$edgeAgent")
                            .WithEnvironment(
                                ("experimentalfeatures__enabled", "true"),
                                ("experimentalfeatures__enableMetrics", "true"),
                                ("PerformanceMetricsUpdateFrequency", "00:00:20"));
                    }, token);
        }
    }
}
