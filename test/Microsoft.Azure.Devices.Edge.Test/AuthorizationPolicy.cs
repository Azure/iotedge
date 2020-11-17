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
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;

    [EndToEnd]
    public class AuthorizationPolicy : SasManualProvisioningFixture
    {
        /// <summary>
        /// Scenario:
        /// - Create a deployment with broker and default authorization policy.
        /// - Create a device and validate that it can connect.
        /// - Update deployment with new blank authorization policy.
        /// - Validate that new device can't connect (unauthorized).
        /// </summary>
        [Test]
        [Ignore("Temporarily disabling flaky test while we figure out what is wrong")]
        public async Task AuthorizationPolicyUpdateTest()
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
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["mqttBroker"] = new
                            {
                                authorizations = new[]
                                {
                                    new
                                    {
                                        identities = new[] { "{{iot:identity}}" },
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
                token);

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

            var leaf = await LeafDevice.CreateAsync(
                DeviceId.Current.Generate(),
                Protocol.Mqtt,
                AuthenticationType.Sas,
                Option.Some(this.runtime.DeviceId),
                false,
                CertificateAuthority.GetQuickstart(),
                this.iotHub,
                token,
                Option.None<string>());

            // verify devices are authorized.
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

            // deploy new blank policy
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
                                authorizations = new object[] { }
                            }
                        });
                },
                token);

            EdgeModule edgeHub2 = deployment2.Modules[ModuleName.EdgeHub];
            await edgeHub2.WaitForReportedPropertyUpdatesAsync(
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
                    DeviceId.Current.Generate(),
                    Protocol.Mqtt,
                    AuthenticationType.Sas,
                    Option.Some(this.runtime.DeviceId),
                    false,
                    CertificateAuthority.GetQuickstart(),
                    this.iotHub,
                    token,
                    Option.None<string>());
                DateTime seekTime = DateTime.Now;
                await leaf.SendEventAsync(token);
                await leaf.WaitForEventsReceivedAsync(seekTime, token);
            });
        }

        /// <summary>
        /// Scenario:
        /// - Create a deployment with broker and two authorization rules:
        ///     allow device1 connect, deny device2 connect.
        /// - Create devices and validate that they can/cannot connect.
        /// </summary>
        [Test]
        [Ignore("Temporarily disabling flaky test while we figure out what is wrong")]
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
                                        identities = new[] { $"{this.iotHub.Hostname}/{deviceId1}" },
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
                                        identities = new[] { $"{this.iotHub.Hostname}/{deviceId2}" },
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
                token);

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
                CertificateAuthority.GetQuickstart(),
                this.iotHub,
                token,
                Option.None<string>());

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
                    CertificateAuthority.GetQuickstart(),
                    this.iotHub,
                    token,
                    Option.None<string>());
                DateTime seekTime = DateTime.Now;
                await leaf.SendEventAsync(token);
                await leaf.WaitForEventsReceivedAsync(seekTime, token);
            });
        }
    }
}
