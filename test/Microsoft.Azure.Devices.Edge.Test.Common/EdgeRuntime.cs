// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class EdgeRuntime
    {
        readonly Option<string> agentImage;
        readonly Option<string> hubImage;
        readonly IotHub iotHub;
        readonly bool optimizeForPerformance;
        readonly Option<Uri> proxy;
        readonly IEnumerable<Registry> registries;

        public string DeviceId { get; }

        public EdgeRuntime(string deviceId, Option<string> agentImage, Option<string> hubImage, Option<Uri> proxy, IEnumerable<Registry> registries, bool optimizeForPerformance, IotHub iotHub)
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
            IotedgeCli cli,
            CancellationToken token,
            bool nestedEdge)
        {
            (string, string)[] hubEnvVar = new (string, string)[] { ("RuntimeLogLevel", "debug"), ("SslProtocols", "tls1.2") };

            if (nestedEdge == true)
            {
                hubEnvVar.Append(("DeviceScopeCacheRefreshDelaySecs", "0"));
            }
            else
            {
                hubEnvVar.Append(("NestedEdgeEnabled", "false"));
            }

            var builder = new EdgeConfigBuilder(this.DeviceId);
            builder.AddRegistries(this.registries);
            builder.AddEdgeAgent(this.agentImage.OrDefault())
                .WithEnvironment(new[] { ("RuntimeLogLevel", "debug") })
                .WithProxy(this.proxy);
            builder.AddEdgeHub(this.hubImage.OrDefault(), this.optimizeForPerformance)
                .WithEnvironment(hubEnvVar)
                .WithProxy(this.proxy);

            addConfig(builder);
            DateTime deployTime = DateTime.Now;
            EdgeConfiguration edgeConfiguration = builder.Build();
            string signedConfig = string.Empty;

            if (!string.IsNullOrEmpty(signedConfig))
            {
                // Convert signed config to ConfigurationContent
                edgeConfiguration.Config = JsonConvert.DeserializeObject<ConfigurationContent>(signedConfig);
            }

            await edgeConfiguration.DeployAsync(this.iotHub, token);
            EdgeModule[] modules = edgeConfiguration.ModuleNames
                .Select(id => new EdgeModule(id, this.DeviceId, this.iotHub))
                .ToArray();
            await EdgeModule.WaitForStatusAsync(modules, EdgeModuleStatus.Running, cli, token);
            await edgeConfiguration.VerifyAsync(this.iotHub, token);
            return new EdgeDeployment(deployTime, modules);
        }

        public Task<EdgeDeployment> DeployConfigurationAsync(IotedgeCli cli, CancellationToken token, bool nestedEdge) =>
            this.DeployConfigurationAsync(_ => { }, cli, token, nestedEdge);
    }
}
