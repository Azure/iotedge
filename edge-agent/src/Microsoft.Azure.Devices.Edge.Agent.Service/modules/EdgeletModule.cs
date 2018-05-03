// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using ModuleIdentityLifecycleManager = Microsoft.Azure.Devices.Edge.Agent.Edgelet.ModuleIdentityLifecycleManager;

    /// <summary>
    /// Initializes Edgelet specific types.
    /// TODO: Right now, it assumes Edgelet supports docker. Need to make it completely implementation agnostic
    /// But that requires IModule implementations to be made generic
    /// </summary>
    public class EdgeletModule : Module
    {
        readonly string deviceId;
        readonly string iotHubHostName;
        readonly string gatewayHostName;
        readonly string edgeletUrl;
        readonly IEnumerable<AuthConfig> dockerAuthConfig;
        readonly Option<UpstreamProtocol> upstreamProtocol;

        public EdgeletModule(string iotHubHostname, string gatewayHostName, string deviceId, string edgeletUri, IEnumerable<AuthConfig> dockerAuthConfig, Option<UpstreamProtocol> upstreamProtocol)
        {
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.gatewayHostName = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.edgeletUrl = Preconditions.CheckNonWhiteSpace(edgeletUri, nameof(edgeletUri));
            this.dockerAuthConfig = Preconditions.CheckNotNull(dockerAuthConfig, nameof(dockerAuthConfig));
            this.upstreamProtocol = Preconditions.CheckNotNull(upstreamProtocol, nameof(upstreamProtocol));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IDeviceClientProvider
            builder.Register(c => new EnvironmentDeviceClientProvider(this.upstreamProtocol))
                .As<IDeviceClientProvider>()
                .SingleInstance();

            // IModuleManager
            builder.Register(c => new ModuleManagementHttpClient(this.edgeletUrl))
                .As<IModuleManager>()
                .As<IIdentityManager>()
                .SingleInstance();

            // IModuleIdentityLifecycleManager
            var identityBuilder = new ModuleIdentityProviderServiceBuilder(this.iotHubHostName, this.deviceId, this.gatewayHostName);
            builder.Register(c => new ModuleIdentityLifecycleManager(c.Resolve<IIdentityManager>(), identityBuilder, this.edgeletUrl))
                .As<IModuleIdentityLifecycleManager>()
                .SingleInstance();

            // ICombinedConfigProvider<CombinedDockerConfig>
            builder.Register(c => new CombinedDockerConfigProvider(this.dockerAuthConfig))
                .As<ICombinedConfigProvider<CombinedDockerConfig>>()
                .SingleInstance();

            // ICommandFactory
            builder.Register(
                    async c =>
                    {
                        var moduleManager = c.Resolve<IModuleManager>();
                        var combinedDockerConfigProvider = c.Resolve<ICombinedConfigProvider<CombinedDockerConfig>>();
                        IConfigSource configSource = await c.Resolve<Task<IConfigSource>>();
                        var edgeletCommandFactory = new EdgeletCommandFactory<CombinedDockerConfig>(moduleManager, configSource, combinedDockerConfigProvider);
                        return new LoggingCommandFactory(edgeletCommandFactory, c.Resolve<ILoggerFactory>()) as ICommandFactory;
                    })
                .As<Task<ICommandFactory>>()
                .SingleInstance();

            // IModuleRuntimeInfoProvider
            builder.Register(c => new RuntimeInfoProvider<DockerReportedConfig>(c.Resolve<IModuleManager>()))
                .As<IRuntimeInfoProvider>()
                .SingleInstance();

            // Task<IEnvironmentProvider>
            builder.Register(
                async c =>
                {
                    var moduleStateStore = c.Resolve<IEntityStore<string, ModuleState>>();
                    var restartPolicyManager = c.Resolve<IRestartPolicyManager>();
                    var runtimeInfoProvider = c.Resolve<IRuntimeInfoProvider>();
                    IEnvironmentProvider dockerEnvironmentProvider = await DockerEnvironmentProvider.CreateAsync(runtimeInfoProvider, moduleStateStore, restartPolicyManager);
                    return dockerEnvironmentProvider;
                })
             .As<Task<IEnvironmentProvider>>()
             .SingleInstance();
        }
    }
}
