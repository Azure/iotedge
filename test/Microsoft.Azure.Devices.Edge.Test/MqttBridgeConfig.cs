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
    public class MqttBridgeConfig : SasManualProvisioningFixture
    {
        /// <summary>
        /// Scenario:
        /// - Create a deployment with broker and a bridge config.
        /// - Update deployment with invalid bridge config.
        /// - Validate the error message.
        /// - Update deployment with new valid bridge config.
        /// - Validate the successful result.
        /// </summary>
        [Test]
        [Category("BrokerRequired")]
        public async Task BridgeConfigUpdateTest()
        {
            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.GetModule(ModuleName.EdgeHub)
                        .WithEnvironment(new[]
                        {
                            ("experimentalFeatures__enabled", "true"),
                            ("experimentalFeatures__mqttBrokerEnabled", "true"),
                        })
                        // deploy with deny policy
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["mqttBroker"] = new
                            {
                                bridges = new[]
                                {
                                    new
                                    {
                                        endpoint = "$upstream",
                                        settings = new[]
                                        {
                                            new
                                            {
                                                direction = "in",
                                                topic = "hello",
                                                inPrefix = "/local/",
                                                outPrefix = "/remote/"
                                            }
                                        }
                                    }
                                }
                            }
                        });
                },
                token,
                this.device.NestedEdge.IsNestedEdge);

            EdgeModule edgeHub = deployment.Modules[ModuleName.EdgeHub];
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

            // deploy invalid bridge config. Missing `settings` property.
            EdgeDeployment deployment2 = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.GetModule(ModuleName.EdgeHub)
                        .WithEnvironment(new[]
                        {
                            ("experimentalFeatures__enabled", "true"),
                            ("experimentalFeatures__mqttBrokerEnabled", "true"),
                        })
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["mqttBroker"] = new
                            {
                                bridges = new[]
                                {
                                    new
                                    {
                                        endpoint = "$upstream"
                                    }
                                }
                            }
                        });
                },
                token,
                this.device.NestedEdge.IsNestedEdge);

            EdgeModule edgeHub2 = deployment.Modules[ModuleName.EdgeHub];
            await edgeHub2.WaitForReportedPropertyUpdatesAsync(
                new
                {
                    properties = new
                    {
                        reported = new
                        {
                            lastDesiredStatus = new
                            {
                                code = 400
                            }
                        }
                    }
                },
                token);

            // deploy new valid bridge config.
            EdgeDeployment deployment3 = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.GetModule(ModuleName.EdgeHub)
                        .WithEnvironment(new[]
                        {
                            ("experimentalFeatures__enabled", "true"),
                            ("experimentalFeatures__mqttBrokerEnabled", "true"),
                        })
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["mqttBroker"] = new
                            {
                                bridges = new[]
                                {
                                    new
                                    {
                                        endpoint = "$upstream",
                                        settings = new object[] { }
                                    }
                                }
                            }
                        });
                },
                token,
                this.device.NestedEdge.IsNestedEdge);

            EdgeModule edgeHub3 = deployment.Modules[ModuleName.EdgeHub];
            await edgeHub3.WaitForReportedPropertyUpdatesAsync(
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
