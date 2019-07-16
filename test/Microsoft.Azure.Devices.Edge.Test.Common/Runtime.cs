// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;

    using Registries = System.Collections.Generic.IEnumerable<(string address, string username, string password)>;

    public class Runtime
    {
        readonly string agentImage;
        readonly string deviceId;
        readonly string hubImage;
        readonly IotHub iotHub;
        readonly bool optimizeForPerformance;
        readonly Option<Uri> proxy;
        readonly Registries registries;

        public Runtime(string deviceId, string agentImage, string hubImage, Option<Uri> proxy, Registries registries, bool optimizeForPerformance, IotHub iotHub)
        {
            this.agentImage = agentImage;
            this.deviceId = deviceId;
            this.hubImage = hubImage;
            this.iotHub = iotHub;
            this.optimizeForPerformance = optimizeForPerformance;
            this.proxy = proxy;
            this.registries = registries;
        }

        // Returns a DateTime representing the moment the configuration was sent to IoT Hub. Some
        // of the test modules begin sending events as soon as they are launched, so this timestamp
        // can be used as a reasonable starting point when listening for events on the IoT hub's
        // Event Hub-compatible endpoint.
        public async Task<DateTime> DeployConfigurationAsync(Action<EdgeConfigBuilder> withConfig, CancellationToken token)
        {
            var builder = new EdgeConfigBuilder(this.deviceId);
            builder.AddRegistryCredentials(this.registries);
            builder.AddEdgeAgent(this.agentImage).WithProxy(this.proxy);
            builder.AddEdgeHub(this.hubImage, this.optimizeForPerformance)
                .WithEnvironment(new[] { ("RuntimeLogLevel", "debug") })
                .WithProxy(this.proxy);

            withConfig(builder);

            DateTime deployTime = DateTime.Now;
            await builder.Build().DeployAsync(this.iotHub, token);

            return deployTime;
        }

        public async Task WaitForModulesRunningAsync(EdgeModule[] modules, CancellationToken token)
        {
            var agent = new EdgeModule("edgeAgent", this.deviceId);
            var hub = new EdgeModule("edgeHub", this.deviceId);

            var allModules = new List<EdgeModule> { agent, hub };
            allModules.AddRange(modules);

            await EdgeModule.WaitForStatusAsync(allModules.ToArray(), EdgeModuleStatus.Running, token);
        }
    }
}
