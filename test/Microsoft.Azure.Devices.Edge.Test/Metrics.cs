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
    using NUnit.Framework;

    public class MetricsValidator : SasManualProvisioningFixture
    {
        string moduleName = "MetricsValidator";
        string imageName = "lefitchereg1.azurecr.io/metrics_validator_test:0.0.1-amd64";

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
                                TempFilterToCloud = "FROM /messages/modules/" + this.moduleName + "/outputs/alertOutput INTO $upstream",
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

            EdgeModule filter = deployment.Modules[this.moduleName];
            string result = await filter.WaitForEventsReceivedAsync(deployment.StartTime, token, "Report");

            File.WriteAllText(".", result);
        }
    }
}
