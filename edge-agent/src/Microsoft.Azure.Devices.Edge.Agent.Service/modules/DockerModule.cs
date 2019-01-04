// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using Core;
    using Docker;
    using Extensions.Logging;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using IoTHub;
    using Storage;
    using Util;

    public class DockerModule : Module
    {
        readonly string deviceId;
        readonly string iotHubHostName;
        readonly string edgeDeviceConnectionString;
        readonly string gatewayHostName;
        readonly Uri dockerHostname;
        readonly IEnumerable<AuthConfig> dockerAuthConfig;
        readonly Option<UpstreamProtocol> upstreamProtocol;
        readonly Option<string> productInfo;

        public DockerModule(
            string edgeDeviceConnectionString,
            string gatewayHostName,
            Uri dockerHostname,
            IEnumerable<AuthConfig> dockerAuthConfig,
            Option<UpstreamProtocol> upstreamProtocol,
            Option<string> productInfo)
        {
            this.edgeDeviceConnectionString = Preconditions.CheckNonWhiteSpace(edgeDeviceConnectionString, nameof(edgeDeviceConnectionString));
            this.gatewayHostName = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
            IotHubConnectionStringBuilder connectionStringParser = IotHubConnectionStringBuilder.Create(this.edgeDeviceConnectionString);
            this.deviceId = connectionStringParser.DeviceId;
            this.iotHubHostName = connectionStringParser.HostName;
            this.dockerHostname = Preconditions.CheckNotNull(dockerHostname, nameof(dockerHostname));
            this.dockerAuthConfig = Preconditions.CheckNotNull(dockerAuthConfig, nameof(dockerAuthConfig));
            this.upstreamProtocol = Preconditions.CheckNotNull(upstreamProtocol, nameof(upstreamProtocol));
            this.productInfo = productInfo;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IDeviceClientProvider
            string edgeAgentConnectionString = $"{this.edgeDeviceConnectionString};{Constants.ModuleIdKey}={Constants.EdgeAgentModuleIdentityName}";
            builder.Register(c => new ModuleClientProvider(edgeAgentConnectionString, this.upstreamProtocol, this.productInfo))
                .As<IModuleClientProvider>()
                .SingleInstance();

            // IServiceClient
            builder.Register(c => new RetryingServiceClient(new ServiceClient(this.edgeDeviceConnectionString, this.deviceId)))
                .As<IServiceClient>()
                .SingleInstance();

            // IModuleIdentityLifecycleManager
            builder.Register(c => new ModuleIdentityLifecycleManager(c.Resolve<IServiceClient>(), this.iotHubHostName, this.deviceId, this.gatewayHostName))
                .As<IModuleIdentityLifecycleManager>()
                .SingleInstance();

            // IDockerClient
            builder.Register(c => new DockerClientConfiguration(this.dockerHostname).CreateClient())
                .As<IDockerClient>()
                .SingleInstance();

            // ICombinedConfigProvider<CombinedDockerConfig>
            builder.Register(c => new CombinedDockerConfigProvider(this.dockerAuthConfig))
                .As<ICombinedConfigProvider<CombinedDockerConfig>>()
                .SingleInstance();

            // ICommandFactory
            builder.Register(
                    async c =>
                    {
                        var dockerClient = c.Resolve<IDockerClient>();
                        var dockerLoggingConfig = c.Resolve<DockerLoggingConfig>();
                        var combinedDockerConfigProvider = c.Resolve<ICombinedConfigProvider<CombinedDockerConfig>>();
                        IConfigSource configSource = await c.Resolve<Task<IConfigSource>>();
                        var dockerFactory = new DockerCommandFactory(dockerClient, dockerLoggingConfig, configSource, combinedDockerConfigProvider);
                        return new LoggingCommandFactory(dockerFactory, c.Resolve<ILoggerFactory>()) as ICommandFactory;
                    })
                .As<Task<ICommandFactory>>()
                .SingleInstance();

            // IRuntimeInfoProvider
            builder.Register(
                    async c =>
                    {
                        IRuntimeInfoProvider runtimeInfoProvider = await RuntimeInfoProvider.CreateAsync(c.Resolve<IDockerClient>());
                        return runtimeInfoProvider;
                    })
                .As<Task<IRuntimeInfoProvider>>()
                .SingleInstance();

            // Task<IEnvironmentProvider>
            builder.Register(
                    async c =>
                    {
                        var moduleStateStore = c.Resolve<IEntityStore<string, ModuleState>>();
                        var restartPolicyManager = c.Resolve<IRestartPolicyManager>();
                        IRuntimeInfoProvider runtimeInfoProvider = await c.Resolve<Task<IRuntimeInfoProvider>>();
                        IEnvironmentProvider dockerEnvironmentProvider = await DockerEnvironmentProvider.CreateAsync(runtimeInfoProvider, moduleStateStore, restartPolicyManager);
                        return dockerEnvironmentProvider;
                    })
                .As<Task<IEnvironmentProvider>>()
                .SingleInstance();
        }
    }
}
