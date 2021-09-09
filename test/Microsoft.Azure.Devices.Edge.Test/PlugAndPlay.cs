// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using NUnit.Framework;

    [EndToEnd]
    public class PlugAndPlay : SasManualProvisioningFixture
    {
        const string TestModelId = "dtmi:edgeE2ETest:TestCapabilityModel;1";
        const string LoadGenModuleName = "loadGenModule";

        [TestCase(Protocol.Mqtt)]
        [TestCase(Protocol.Amqp)]
        [Category("LegacyMqttRequired")]
        public async Task PlugAndPlayDeviceClient(Protocol protocol)
        {
            CancellationToken token = this.TestToken;
            string leafDeviceId = DeviceId.Current.Generate();

            Action<EdgeConfigBuilder> config = this.BuildAddEdgeHubConfig(protocol);
            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                config,
                token,
                Context.Current.NestedEdge);

            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                protocol,
                AuthenticationType.Sas,
                Option.Some(this.runtime.DeviceId),
                false,
                this.ca,
                this.IotHub,
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

        [TestCase(Protocol.Mqtt)]
        [TestCase(Protocol.Amqp)]
        [Category("LegacyMqttRequired")]
        [Test]
        public async Task PlugAndPlayModuleClient(Protocol protocol)
        {
            CancellationToken token = this.TestToken;

            string loadGenImage = Context.Current.LoadGenImage.Expect(() => new ArgumentException("loadGenImage parameter is required for Priority Queues test"));

            Action<EdgeConfigBuilder> config = this.BuildAddEdgeHubConfig(protocol) + this.BuildAddLoadGenConfig(protocol, loadGenImage);
            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                config,
                token,
                Context.Current.NestedEdge);

            EdgeModule filter = deployment.Modules[LoadGenModuleName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
            await this.ValidateIdentity(this.runtime.DeviceId, Option.Some(LoadGenModuleName), TestModelId, token);
        }

        async Task ValidateIdentity(string deviceId, Option<string> moduleId, string expectedModelId, CancellationToken token)
        {
            Twin twin = await this.IotHub.GetTwinAsync(deviceId, moduleId, token);
            string actualModelId = twin.ModelId;
            Assert.AreEqual(expectedModelId, actualModelId);
        }

        private Action<EdgeConfigBuilder> BuildAddLoadGenConfig(Protocol protocol, string loadGenImage)
        {
            return new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(LoadGenModuleName, loadGenImage)
                    .WithEnvironment(new[]
                    {
                            ("testStartDelay", "00:00:00"),
                            ("messageFrequency", "00:00:00.5"),
                            ("transportType", protocol.ToString()),
                            ("modelId", TestModelId)
                    });
                });
        }

        private Action<EdgeConfigBuilder> BuildAddEdgeHubConfig(Protocol protocol)
        {
            return new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.GetModule(ModuleName.EdgeHub).WithEnvironment(new[] { ("UpstreamProtocol", protocol.ToString()) });
                });
        }
    }
}
