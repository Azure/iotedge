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

    [EndToEnd]
    public class Metrics : SasManualProvisioningFixture
    {
        const string ModuleName = "MetricsValidator";
        const string EdgeAgentBaseImage = "mcr.microsoft.com/azureiotedge-agent:1.0";

        [Test]
        public async Task ValidateMetrics()
        {
            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule("tempSensor999", Context.Current.TempSensorImage.Expect(() => new Exception("Oh no!")))
                        .WithEnvironment(new[] { ("MessageCount", "1") });

                    builder.GetModule("$edgeHub")
                        .WithEnvironment(("experimentalfeatures__enabled", "true"));
                },
                token);

            EdgeModule sensor = deployment.Modules["tempSensor999"];
            await sensor.WaitForEventsReceivedAsync(deployment.StartTime, token);

            await Task.Delay(TimeSpan.FromMinutes(4));
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

                        var edgeHub = builder.GetModule("$edgeHub")
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
