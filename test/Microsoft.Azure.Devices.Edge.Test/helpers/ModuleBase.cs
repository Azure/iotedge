// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;

    public class ModuleBase : TestBase
    {
        protected string deviceId;
        protected IotHub iotHub;

        [SetUp]
        async Task BeforeEachAsync()
        {
            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.Proxy);

            this.deviceId = (await EdgeDevice
                .GetIdentityAsync(Context.Current.DeviceId, this.iotHub, this.cts.Token))
                .Map(device => device.Id)
                .Expect(() => new Exception("Device should have already been created in setup fixture"));
        }

        protected async Task<DateTime> DeployConfigurationAsync(Action<EdgeConfigBuilder> withConfig, CancellationToken token)
        {
            string agentImage = Context.Current.EdgeAgentImage.Expect(() => new ArgumentException());
            string hubImage = Context.Current.EdgeHubImage.Expect(() => new ArgumentException());
            Option<Uri> proxy = Context.Current.Proxy;

            var builder = new EdgeConfigBuilder(this.deviceId);
            builder.AddRegistryCredentials(Context.Current.Registries);
            builder.AddEdgeAgent(agentImage).WithProxy(proxy);
            builder.AddEdgeHub(hubImage, Context.Current.OptimizeForPerformance)
                .WithEnvironment(new[] { ("RuntimeLogLevel", "debug") })
                .WithProxy(proxy);

            withConfig(builder);

            DateTime deployTime = DateTime.Now;
            await builder.Build().DeployAsync(this.iotHub, token);

            return deployTime;
        }

        protected async Task WaitForModulesRunningAsync(EdgeModule[] modules, CancellationToken token)
        {
            var agent = new EdgeModule("edgeAgent", this.deviceId);
            var hub = new EdgeModule("edgeHub", this.deviceId);

            var allModules = new List<EdgeModule> { agent, hub };
            allModules.AddRange(modules);

            await EdgeModule.WaitForStatusAsync(allModules.ToArray(), EdgeModuleStatus.Running, token);
        }
    }
}
