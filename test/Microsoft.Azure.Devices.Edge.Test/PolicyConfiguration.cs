// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;

    [EndToEnd]
    public class PolicyConfiguration : SasManualProvisioningFixture
    {
        [Test]
        public async Task PolicyConfigurationTwinUpdate()
        {
            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.GetModule(ModuleName.EdgeHub)
                        .WithEnvironment(new[]
                        {
                            ("experimentalFeatures:enabled", "true"),
                            ("experimentalFeatures:mqttBrokerEnabled", "true"),
                        })
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["mqttBroker"] = new
                            {
                                authorization = new[]
                                {
                                    new
                                    {
                                        identities = new[] { "device1" },
                                        allow = new[]
                                        {
                                            new
                                            {
                                                operations = new[] { "mqtt:publish" },
                                                resources = new[] { "topic/a" }
                                            }
                                        }
                                    }
                                }
                            }
                        });
                },
                token);

            EdgeModule edgeHub = deployment.Modules["edgeHub"];
            await edgeHub.WaitForReportedPropertyUpdatesAsync(
                new
                {
                    properties = new
                    {
                        reported = new
                        {
                            lastDesiredStatus = new
                            {
                                code = 200,
                                description = string.Empty
                            }
                        }
                    }
                },
                token);
        }
    }
}
