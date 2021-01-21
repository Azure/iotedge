// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Serilog;

    [EndToEnd]
    public class PlugAndPlay : SasManualProvisioningFixture
    {
        const string TestModelId = "dtmi:edgeE2ETest:TestCapabilityModel;1";
        const string LoadGenModuleName = "loadGenModule";

        [TestCase(Protocol.Mqtt, false)]
        [TestCase(Protocol.Amqp, false)]
        [TestCase(Protocol.Mqtt, true)]
        [TestCase(Protocol.Amqp, true)]
        public async Task PlugAndPlayDeviceClient(Protocol protocol, bool brokerOn)
        {
            CancellationToken token = this.TestToken;
            string leafDeviceId = DeviceId.Current.Generate();
            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    if (brokerOn)
                    {
                        this.AddBrokerToDeployment(builder);
                    }

                    builder.GetModule(ModuleName.EdgeHub).WithEnvironment(new[] { ("UpstreamProtocol", protocol.ToString()) });
                },
                token,
                Context.Current.NestedEdge);

            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                protocol,
                AuthenticationType.Sas,
                Option.Some(this.runtime.DeviceId),
                false,
                this.ca,
                this.iotHub,
                Context.Current.Hostname.GetOrElse(Dns.GetHostName().ToLower()),
                token,
                Option.Some(TestModelId),
                Context.Current.NestedEdge);

            await TryFinally.DoAsync(
                async () =>
                {
                    DateTime seekTime = DateTime.Now;
                    await leaf.SendEventAsync(token);
                    await leaf.WaitForEventsReceivedAsync(seekTime, token);
                    await this.ValidateIdentity(leafDeviceId, Option.None<string>(), TestModelId, token);
                },
                async () =>
                {
                    await leaf.DeleteIdentityAsync(token);
                });
        }

        [TestCase(Protocol.Mqtt, false)]
        [TestCase(Protocol.Amqp, false)]
        [TestCase(Protocol.Mqtt, true)]
        [TestCase(Protocol.Amqp, true)]
        [Test]
        public async Task PlugAndPlayModuleClient(Protocol protocol, bool brokerOn)
        {
            CancellationToken token = this.TestToken;
            string loadGenImage = Context.Current.LoadGenImage.Expect(() => new ArgumentException("loadGenImage parameter is required for Priority Queues test"));
            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    if (brokerOn)
                    {
                        this.AddBrokerToDeployment(builder);
                    }

                    builder.GetModule(ModuleName.EdgeHub).WithEnvironment(new[] { ("UpstreamProtocol", protocol.ToString()) });
                    builder.AddModule(LoadGenModuleName, loadGenImage)
                    .WithEnvironment(new[]
                    {
                            ("testStartDelay", "00:00:00"),
                            ("messageFrequency", "00:00:00.5"),
                            ("transportType", protocol.ToString()),
                            ("modelId", TestModelId)
                    });
                },
                token,
                Context.Current.NestedEdge);

            EdgeModule filter = deployment.Modules[LoadGenModuleName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
            await this.ValidateIdentity(this.runtime.DeviceId, Option.Some(LoadGenModuleName), TestModelId, token);
        }

        EdgeConfigBuilder AddBrokerToDeployment(EdgeConfigBuilder builder)
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
            return builder;
        }

        async Task ValidateIdentity(string deviceId, Option<string> moduleId, string expectedModelId, CancellationToken token)
        {
            Twin twin = await this.iotHub.GetTwinAsync(deviceId, moduleId, token);
            string actualModelId = twin.ModelId;
            Assert.AreEqual(expectedModelId, actualModelId);
        }
    }
}
