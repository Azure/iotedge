// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Registries = System.Collections.Generic.IEnumerable<(string address, string username, string password)>;

    public class EdgeRuntime
    {
        readonly Option<string> agentImage;
        readonly Option<string> hubImage;
        readonly IotHub iotHub;
        readonly bool optimizeForPerformance;
        readonly Option<Uri> proxy;
        readonly Registries registries;

        public string DeviceId { get; }

        public EdgeRuntime(string deviceId, Option<string> agentImage, Option<string> hubImage, Option<Uri> proxy, Registries registries, bool optimizeForPerformance, IotHub iotHub)
        {
            this.agentImage = agentImage;
            this.hubImage = hubImage;
            this.iotHub = iotHub;
            this.optimizeForPerformance = optimizeForPerformance;
            this.proxy = proxy;
            this.registries = registries;

            this.DeviceId = deviceId;
        }

        // DeployConfigurationAsync builds a configuration that includes Edge Agent, Edge Hub, and
        // anything added by addConfig(). It deploys the config and waits for the edge device to
        // receive it and start up all the modules.
        public async Task<EdgeDeployment> DeployConfigurationAsync(
            Action<EdgeConfigBuilder> addConfig,
            CancellationToken token,
            bool stageSystemModules = true)
        {
            var builder = new EdgeConfigBuilder(this.DeviceId);
            builder.AddRegistryCredentials(this.registries);
            builder.AddEdgeAgent(this.agentImage.OrDefault())
                .WithEnvironment(new[] { ("RuntimeLogLevel", "debug") })
                .WithProxy(this.proxy);
            builder.AddEdgeHub(this.hubImage.OrDefault(), this.optimizeForPerformance)
                .WithEnvironment(new[] { ("RuntimeLogLevel", "debug") })
                .WithProxy(this.proxy);

            addConfig(builder);

            DateTime deployTime = DateTime.Now;
            var finalModules = new EdgeModule[] { };
            IEnumerable<EdgeConfiguration> configs = builder.Build(stageSystemModules).ToArray();
            foreach (EdgeConfiguration edgeConfiguration in configs)
            {
                await edgeConfiguration.DeployAsync(this.iotHub, token);
                EdgeModule[] modules = edgeConfiguration.ModuleNames
                    .Select(id => new EdgeModule(id, this.DeviceId, this.iotHub))
                    .ToArray();
                await EdgeModule.WaitForStatusAsync(modules, EdgeModuleStatus.Running, token);
                await edgeConfiguration.VerifyAsync(this.iotHub, token);
                finalModules = modules;
            }

            return new EdgeDeployment(deployTime, finalModules);
        }

        public Task<EdgeDeployment> DeployConfigurationAsync(CancellationToken token) =>
            this.DeployConfigurationAsync(_ => { }, token);
    }
}
