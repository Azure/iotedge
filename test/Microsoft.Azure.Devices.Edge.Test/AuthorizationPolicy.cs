// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using NUnit.Framework;

    [EndToEnd]
    public class AuthorizationPolicy : SasManualProvisioningFixture
    {
        /// <summary>
        /// Scenario:
        /// - Create a deployment with broker and a policy that denies the connection.
        /// - Create a device and validate that it cannot connect.
        /// - Update deployment with new policy that allows the connection.
        /// - Validate that new device can connect.
        /// </summary>
        /// <returns><see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        [Category("BrokerRequired")]
        public async Task AuthorizationPolicyUpdateTest()
        {
            CancellationToken token = this.TestToken;

            string deviceId1 = DeviceId.Current.Generate();
            string deviceId2 = DeviceId.Current.Generate();

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
                                authorizations = new[]
                                {
                                    new
                                    {
                                        identities = new[] { $"{this.IotHub.Hostname}/{deviceId1}" },
                                        deny = new[]
                                        {
                                            new
                                            {
                                                operations = new[] { "mqtt:connect" }
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

            // verify devices are not authorized after policy update.
            Assert.ThrowsAsync<UnauthorizedException>(async () =>
            {
                var leaf = await LeafDevice.CreateAsync(
                    deviceId1,
                    Protocol.Mqtt,
                    AuthenticationType.Sas,
                    Option.Some(this.runtime.DeviceId),
                    false,
                    this.ca,
                    this.IotHub,
                    this.device.NestedEdge.DeviceHostname,
                    token,
                    Option.None<string>(),
                    this.device.NestedEdge.IsNestedEdge);
                DateTime seekTime = DateTime.Now;
                await leaf.SendEventAsync(token);
                await leaf.WaitForEventsReceivedAsync(seekTime, token);
            });

            // deploy new allow policy
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
                                authorizations = new[]
                                {
                                    new
                                    {
                                        identities = new[] { $"{this.IotHub.Hostname}/{deviceId2}" },
                                        allow = new[]
                                        {
                                            new
                                            {
                                                operations = new[] { "mqtt:connect" }
                                            }
                                        }
                                    }
                                }
                            }
                        });
                },
                token,
                this.device.NestedEdge.IsNestedEdge);

            // Create device manually. We can't use LeafDevice.CreateAsync() since it is not
            // idempotent and cannot be retried reliably.
            Devices.Device edge = await this.IotHub.GetDeviceIdentityAsync(this.runtime.DeviceId, token);
            Devices.Device leaf = new Devices.Device(deviceId2)
            {
                Authentication = new AuthenticationMechanism
                {
                    Type = AuthenticationType.Sas
                },
                Scope = edge.Scope
            };

            leaf = await this.IotHub.CreateDeviceIdentityAsync(leaf, token);

            string connectionString =
                $"HostName={this.IotHub.Hostname};" +
                $"DeviceId={leaf.Id};" +
                $"SharedAccessKey={leaf.Authentication.SymmetricKey.PrimaryKey};" +
                $"GatewayHostName={this.device.NestedEdge.DeviceHostname}";

            // There is no reliable way to signal when the policy
            // is updated in $edgehub, so need to retry several times.
            //
            // DefaultProgressive => 55 sec max.
            await RetryPolicy.DefaultProgressive.ExecuteAsync(
                async () =>
            {
                using var client = DeviceClient.CreateFromConnectionString(connectionString, Client.TransportType.Mqtt);
                await client.OpenAsync();
            }, token);

            await this.IotHub.DeleteDeviceIdentityAsync(leaf, token);
        }

        /// <summary>
        /// Scenario:
        /// - Create a deployment with broker and two authorization rules:
        ///     allow device1 connect, deny device2 connect.
        /// - Create devices and validate that they can/cannot connect.
        /// </summary>
        /// <returns><see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        [Category("BrokerRequired")]
        public async Task AuthorizationPolicyExplicitPolicyTest()
        {
            CancellationToken token = this.TestToken;

            string deviceId1 = DeviceId.Current.Generate();
            string deviceId2 = DeviceId.Current.Generate();

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
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
                                authorizations = new dynamic[]
                                {
                                    new
                                    {
                                        identities = new[] { $"{this.IotHub.Hostname}/{deviceId1}" },
                                        allow = new[]
                                        {
                                            new
                                            {
                                                operations = new[] { "mqtt:connect" }
                                            }
                                        }
                                    },
                                    new
                                    {
                                        identities = new[] { $"{this.IotHub.Hostname}/{deviceId2}" },
                                        deny = new[]
                                        {
                                            new
                                            {
                                                operations = new[] { "mqtt:connect" }
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

            // verify device1 is authorized
            var leaf = await LeafDevice.CreateAsync(
                deviceId1,
                Protocol.Mqtt,
                AuthenticationType.Sas,
                Option.Some(this.runtime.DeviceId),
                false,
                this.ca,
                this.IotHub,
                this.device.NestedEdge.DeviceHostname,
                token,
                Option.None<string>(),
                this.device.NestedEdge.IsNestedEdge);

            await TryFinally.DoAsync(
                async () =>
                {
                    DateTime seekTime = DateTime.Now;
                    await leaf.SendEventAsync(token);
                    await leaf.WaitForEventsReceivedAsync(seekTime, token);
                },
                async () =>
                {
                    await leaf.DeleteIdentityAsync(token);
                });

            // verify device2 is not authorized
            Assert.ThrowsAsync<UnauthorizedException>(async () =>
            {
                var leaf = await LeafDevice.CreateAsync(
                    deviceId2,
                    Protocol.Mqtt,
                    AuthenticationType.Sas,
                    Option.Some(this.runtime.DeviceId),
                    false,
                    this.ca,
                    this.IotHub,
                    this.device.NestedEdge.DeviceHostname,
                    token,
                    Option.None<string>(),
                    this.device.NestedEdge.IsNestedEdge);
                DateTime seekTime = DateTime.Now;
                await leaf.SendEventAsync(token);
                await leaf.WaitForEventsReceivedAsync(seekTime, token);
            });
        }
    }
}
