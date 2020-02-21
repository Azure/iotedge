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
        readonly string deviceId;
        readonly Option<string> hubImage;
        readonly IotHub iotHub;
        readonly bool optimizeForPerformance;
        readonly Option<Uri> proxy;
        readonly Registries registries;

        public EdgeRuntime(string deviceId, Option<string> agentImage, Option<string> hubImage, Option<Uri> proxy, Registries registries, bool optimizeForPerformance, IotHub iotHub)
        {
            this.agentImage = agentImage;
            this.deviceId = deviceId;
            this.hubImage = hubImage;
            this.iotHub = iotHub;
            this.optimizeForPerformance = optimizeForPerformance;
            this.proxy = proxy;
            this.registries = registries;
        }

        // DeployConfigurationAsync builds a configuration that includes Edge Agent, Edge Hub, and
        // anything added by addConfig(). It deploys the config and waits for the edge device to
        // receive it and start up all the modules.
        public async Task<EdgeDeployment> DeployConfigurationAsync(Action<EdgeConfigBuilder> addConfig, CancellationToken token)
        {
            var builder = new EdgeConfigBuilder(this.deviceId);
            builder.AddRegistryCredentials(this.registries);
            builder.AddEdgeAgent(this.agentImage.OrDefault())
                .WithEnvironment(new[] { ("RuntimeLogLevel", "debug") })
                .WithProxy(this.proxy);
            builder.AddEdgeHub(this.hubImage.OrDefault(), this.optimizeForPerformance)
                .WithEnvironment(new[] { ("RuntimeLogLevel", "debug") })
                .WithProxy(this.proxy);

            addConfig(builder);

            DateTime deployTime = DateTime.Now;
            IEnumerable<EdgeConfiguration> configs = builder.BuildConfigurationStages().ToArray();
            foreach (EdgeConfiguration edgeConfiguration in configs)
            {
                await edgeConfiguration.DeployAsync(this.iotHub, token);
                await Task.Delay(TimeSpan.FromSeconds(10));
            }

            // The last configuration will contain all modules including $edgeHub and $edgeAgent
            List<EdgeModule> modules = configs.Last().ModuleNames
                .Select(id => new EdgeModule(id, this.deviceId, this.iotHub))
                .ToList();
            await EdgeModule.WaitForStatusAsync(modules, EdgeModuleStatus.Running, token);

            return new EdgeDeployment(deployTime, modules);
        }

        public Task<EdgeDeployment> DeployConfigurationAsync(CancellationToken token) =>
            this.DeployConfigurationAsync(_ => { }, token);
    }
}
