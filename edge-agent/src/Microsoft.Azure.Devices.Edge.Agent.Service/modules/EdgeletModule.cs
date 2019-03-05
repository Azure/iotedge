// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker;
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
        readonly Uri managementUri;
        readonly Uri workloadUri;
        readonly string apiVersion;
        readonly IEnumerable<AuthConfig> dockerAuthConfig;
        readonly Option<UpstreamProtocol> upstreamProtocol;
        readonly Option<IWebProxy> proxy;
        readonly Option<string> productInfo;

        public EdgeletModule(IAgentAppSettings appSettings)
        {
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(appSettings.IoTHubHostName, nameof(appSettings.IoTHubHostName));
            this.gatewayHostName = Preconditions.CheckNonWhiteSpace(appSettings.EdgeDeviceHostName, nameof(appSettings.EdgeDeviceHostName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(appSettings.DeviceId, nameof(appSettings.DeviceId));

            this.managementUri = Preconditions.CheckNotNull(new Uri(appSettings.ManagementUri), nameof(appSettings.ManagementUri));
            this.workloadUri = Preconditions.CheckNotNull(new Uri(appSettings.WorkloadUri), nameof(appSettings.WorkloadUri));
            this.apiVersion = Preconditions.CheckNonWhiteSpace(appSettings.ApiVersion, nameof(appSettings.ApiVersion));
            this.dockerAuthConfig = Preconditions.CheckNotNull(appSettings.DockerRegistryAuthConfigs, nameof(appSettings.DockerRegistryAuthConfigs));
            this.upstreamProtocol = appSettings.UpstreamProtocol;
            this.proxy = appSettings.HttpsProxy;
            this.productInfo = Option.Maybe(appSettings.ProductInfo);
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IModuleClientProvider
            builder.Register(c => new EnvironmentModuleClientProvider(this.upstreamProtocol, this.proxy, this.productInfo))
                .As<IModuleClientProvider>()
                .SingleInstance();

            // IModuleManager
            builder.Register(c => new ModuleManagementHttpClient(this.managementUri, this.apiVersion, Constants.EdgeletClientApiVersion))
                .As<IModuleManager>()
                .As<IIdentityManager>()
                .SingleInstance();

            // IModuleIdentityLifecycleManager
            var identityBuilder = new ModuleIdentityProviderServiceBuilder(this.iotHubHostName, this.deviceId, this.gatewayHostName);
            builder.Register(c => new ModuleIdentityLifecycleManager(c.Resolve<IIdentityManager>(), identityBuilder, this.workloadUri))
                .As<IModuleIdentityLifecycleManager>()
                .SingleInstance();

            // ICombinedConfigProvider<CombinedDockerConfig>
            builder.Register(
                    async c =>
                    {
                        IConfigSource configSource = await c.Resolve<Task<IConfigSource>>();
                        return new CombinedEdgeletConfigProvider(this.dockerAuthConfig, configSource) as ICombinedConfigProvider<CombinedDockerConfig>;
                    })
                .As<Task<ICombinedConfigProvider<CombinedDockerConfig>>>()
                .SingleInstance();

            // ICommandFactory
            builder.Register(
                    async c =>
                    {
                        var moduleManager = c.Resolve<IModuleManager>();
                        ICombinedConfigProvider<CombinedDockerConfig> combinedDockerConfigProvider = await c.Resolve<Task<ICombinedConfigProvider<CombinedDockerConfig>>>();
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
