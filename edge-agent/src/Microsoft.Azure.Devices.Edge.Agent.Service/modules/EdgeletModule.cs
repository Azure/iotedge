// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.DeviceManager;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;
    using ModuleIdentityLifecycleManager = Microsoft.Azure.Devices.Edge.Agent.Edgelet.ModuleIdentityLifecycleManager;

    /// <summary>
    /// Initializes Edgelet specific types.
    /// TODO: Right now, it assumes Edgelet supports docker. Need to make it completely implementation agnostic
    /// But that requires IModule implementations to be made generic.
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
        readonly string productInfo;
        readonly bool closeOnIdleTimeout;
        readonly TimeSpan idleTimeout;
        readonly TimeSpan performanceMetricsUpdateFrequency;

        public EdgeletModule(
            string iotHubHostname,
            string gatewayHostName,
            string deviceId,
            Uri managementUri,
            Uri workloadUri,
            string apiVersion,
            IEnumerable<AuthConfig> dockerAuthConfig,
            Option<UpstreamProtocol> upstreamProtocol,
            Option<IWebProxy> proxy,
            string productInfo,
            bool closeOnIdleTimeout,
            TimeSpan idleTimeout,
            TimeSpan performanceMetricsUpdateFrequency)
        {
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.gatewayHostName = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.managementUri = Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            this.workloadUri = Preconditions.CheckNotNull(workloadUri, nameof(workloadUri));
            this.apiVersion = Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));
            this.dockerAuthConfig = Preconditions.CheckNotNull(dockerAuthConfig, nameof(dockerAuthConfig));
            this.upstreamProtocol = upstreamProtocol;
            this.proxy = proxy;
            this.productInfo = Preconditions.CheckNotNull(productInfo, nameof(productInfo));
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            this.idleTimeout = idleTimeout;
            this.performanceMetricsUpdateFrequency = performanceMetricsUpdateFrequency;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IModuleClientProvider
            builder.Register(
                    c => new ModuleClientProvider(
                        c.Resolve<ISdkModuleClientProvider>(),
                        this.upstreamProtocol,
                        this.proxy,
                        this.productInfo,
                        this.closeOnIdleTimeout,
                        this.idleTimeout))
                .As<IModuleClientProvider>()
                .SingleInstance();

            // IModuleManager
            builder.Register(c => new ModuleManagementHttpClient(this.managementUri, this.apiVersion, Constants.EdgeletClientApiVersion))
                .As<IModuleManager>()
                .As<IIdentityManager>()
                .As<IDeviceManager>()
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
                        var combinedDockerConfigProviderTask = c.Resolve<Task<ICombinedConfigProvider<CombinedDockerConfig>>>();
                        var configSourceTask = c.Resolve<Task<IConfigSource>>();
                        var metricsProvider = c.Resolve<IMetricsProvider>();
                        var loggerFactory = c.Resolve<ILoggerFactory>();
                        IConfigSource configSource = await configSourceTask;
                        ICombinedConfigProvider<CombinedDockerConfig> combinedDockerConfigProvider = await combinedDockerConfigProviderTask;
                        ICommandFactory factory = new EdgeletCommandFactory<CombinedDockerConfig>(moduleManager, configSource, combinedDockerConfigProvider);
                        factory = new MetricsCommandFactory(factory, metricsProvider);
                        return new LoggingCommandFactory(factory, loggerFactory) as ICommandFactory;
                    })
                .As<Task<ICommandFactory>>()
                .SingleInstance();

            // Task<IRuntimeInfoProvider>
            builder.Register(c => Task.FromResult(new RuntimeInfoProvider<DockerReportedConfig>(c.Resolve<IModuleManager>()) as IRuntimeInfoProvider))
                .As<Task<IRuntimeInfoProvider>>()
                .SingleInstance();

            // Task<IEnvironmentProvider>
            builder.Register(
                    async c =>
                    {
                        var moduleStateStore = await c.Resolve<Task<IEntityStore<string, ModuleState>>>();
                        var restartPolicyManager = c.Resolve<IRestartPolicyManager>();
                        IRuntimeInfoProvider runtimeInfoProvider = await c.Resolve<Task<IRuntimeInfoProvider>>();
                        IEnvironmentProvider dockerEnvironmentProvider = await DockerEnvironmentProvider.CreateAsync(runtimeInfoProvider, moduleStateStore, restartPolicyManager, CancellationToken.None);
                        return dockerEnvironmentProvider;
                    })
                .As<Task<IEnvironmentProvider>>()
                .SingleInstance();

            // SystemResourcesMetrics
            builder.Register(c => new SystemResourcesMetrics(c.Resolve<IMetricsProvider>(), c.Resolve<IModuleManager>().GetSystemResourcesAsync, this.apiVersion, this.performanceMetricsUpdateFrequency))
                .SingleInstance();
        }
    }
}
