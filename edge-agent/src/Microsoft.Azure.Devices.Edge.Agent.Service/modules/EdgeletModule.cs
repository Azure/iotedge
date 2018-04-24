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
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

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

        public EdgeletModule(string edgeDeviceConnectionString, string gatewayHostName, string edgeletUri, IEnumerable<AuthConfig> dockerAuthConfig)
        {
            Preconditions.CheckNonWhiteSpace(edgeDeviceConnectionString, nameof(edgeDeviceConnectionString));
            this.gatewayHostName = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
            IotHubConnectionStringBuilder connectionStringParser = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            this.deviceId = connectionStringParser.DeviceId;
            this.iotHubHostName = connectionStringParser.HostName;
            this.edgeletUrl = Preconditions.CheckNonWhiteSpace(edgeletUri, nameof(edgeletUri));
            this.dockerAuthConfig = Preconditions.CheckNotNull(dockerAuthConfig, nameof(dockerAuthConfig));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IModuleManager
            builder.Register(c => new ModuleManagementHttpClient(this.edgeletUrl))
                .As<IModuleManager>()
                .As<IIdentityManager>()
                .SingleInstance();

            // IModuleIdentityLifecycleManager
            builder.Register(c => new ModuleIdentityLifecycleManager(c.Resolve<IIdentityManager>(), new ModuleConnectionStringBuilder(this.iotHubHostName, this.deviceId), this.gatewayHostName))
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
                    IEntityStore<string, ModuleState> moduleStateStore = c.Resolve<IEntityStore<string, ModuleState>>();
                    IRestartPolicyManager restartPolicyManager = c.Resolve<IRestartPolicyManager>();
                    IRuntimeInfoProvider runtimeInfoProvider = c.Resolve<IRuntimeInfoProvider>();
                    IEnvironmentProvider dockerEnvironmentProvider = await DockerEnvironmentProvider.CreateAsync(runtimeInfoProvider, moduleStateStore, restartPolicyManager);
                    return dockerEnvironmentProvider;
                })
             .As<Task<IEnvironmentProvider>>()
             .SingleInstance();
        }
    }
}
