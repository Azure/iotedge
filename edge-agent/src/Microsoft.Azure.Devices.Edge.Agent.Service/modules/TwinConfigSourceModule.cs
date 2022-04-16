// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.DeviceManager;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Reporters;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public class TwinConfigSourceModule : Module
    {
        const string DockerType = "docker";
        readonly IConfiguration configuration;
        readonly VersionInfo versionInfo;
        readonly TimeSpan configRefreshFrequency;
        readonly string deviceId;
        readonly string iotHubHostName;
        readonly bool enableStreams;
        readonly TimeSpan requestTimeout;
        readonly ExperimentalFeatures experimentalFeatures;

        public TwinConfigSourceModule(
            string iotHubHostname,
            string deviceId,
            IConfiguration config,
            VersionInfo versionInfo,
            TimeSpan configRefreshFrequency,
            bool enableStreams,
            TimeSpan requestTimeout,
            ExperimentalFeatures experimentalFeatures)
        {
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.configuration = Preconditions.CheckNotNull(config, nameof(config));
            this.versionInfo = Preconditions.CheckNotNull(versionInfo, nameof(versionInfo));
            this.configRefreshFrequency = configRefreshFrequency;
            this.enableStreams = enableStreams;
            this.requestTimeout = requestTimeout;
            this.experimentalFeatures = experimentalFeatures;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // ILogsUploader
            builder.Register(c => new AzureBlobRequestsUploader(this.iotHubHostName, this.deviceId))
                .As<IRequestsUploader>()
                .SingleInstance();

            // Task<ILogsProvider>
            builder.Register(
                async c =>
                {
                    var logsProcessor = new LogsProcessor(new LogMessageParser(this.iotHubHostName, this.deviceId));
                    IRuntimeInfoProvider runtimeInfoProvider = await c.Resolve<Task<IRuntimeInfoProvider>>();
                    return new LogsProvider(runtimeInfoProvider, logsProcessor) as ILogsProvider;
                })
                .As<Task<ILogsProvider>>()
                .SingleInstance();

            // IRequestManager
            builder.Register(
                c =>
                {
                    var requestHandlers = new List<IRequestHandler>
                    {
                        new PingRequestHandler(),
                        new TaskStatusRequestHandler()
                    };
                    return new RequestManager(requestHandlers, this.requestTimeout) as IRequestManager;
                })
                .As<IRequestManager>()
                .SingleInstance();

            // Task<IRequestHandler> - LogsUploadRequestHandler
            builder.Register(
                    async c =>
                    {
                        var requestUploader = c.Resolve<IRequestsUploader>();
                        var runtimeInfoProviderTask = c.Resolve<Task<IRuntimeInfoProvider>>();
                        var logsProviderTask = c.Resolve<Task<ILogsProvider>>();
                        IRuntimeInfoProvider runtimeInfoProvider = await runtimeInfoProviderTask;
                        ILogsProvider logsProvider = await logsProviderTask;
                        return new ModuleLogsUploadRequestHandler(requestUploader, logsProvider, runtimeInfoProvider) as IRequestHandler;
                    })
                .As<Task<IRequestHandler>>()
                .SingleInstance();

            // Task<IRequestHandler> - LogsRequestHandler
            builder.Register(
                    async c =>
                    {
                        var runtimeInfoProviderTask = c.Resolve<Task<IRuntimeInfoProvider>>();
                        var logsProviderTask = c.Resolve<Task<ILogsProvider>>();
                        IRuntimeInfoProvider runtimeInfoProvider = await runtimeInfoProviderTask;
                        ILogsProvider logsProvider = await logsProviderTask;
                        return new ModuleLogsRequestHandler(logsProvider, runtimeInfoProvider) as IRequestHandler;
                    })
                .As<Task<IRequestHandler>>()
                .SingleInstance();

            // Task<IRequestHandler> - SupportBundleRequestHandler
            builder.Register(
                    c =>
                    {
                        IRequestHandler handler = new SupportBundleRequestHandler(c.Resolve<IModuleManager>().GetSupportBundle, c.Resolve<IRequestsUploader>(), this.iotHubHostName);
                        return Task.FromResult(handler);
                    })
                .As<Task<IRequestHandler>>()
                .SingleInstance();

            // Task<IRequestHandler> - RestartRequestHandler
            builder.Register(
                    async c =>
                    {
                        var environmentProviderTask = c.Resolve<Task<IEnvironmentProvider>>();
                        var commandFactoryTask = c.Resolve<Task<ICommandFactory>>();
                        var configSourceTask = c.Resolve<Task<IConfigSource>>();
                        IEnvironmentProvider environmentProvider = await environmentProviderTask;
                        ICommandFactory commandFactory = await commandFactoryTask;
                        IConfigSource configSource = await configSourceTask;
                        return new RestartRequestHandler(environmentProvider, configSource, commandFactory) as IRequestHandler;
                    })
                .As<Task<IRequestHandler>>()
                .SingleInstance();

            // ISdkModuleClientProvider
            builder.Register(c => new SdkModuleClientProvider())
                .As<ISdkModuleClientProvider>()
                .SingleInstance();

            // IEdgeAgentConnection
            builder.Register(
                c =>
                {
                    var serde = c.Resolve<ISerde<DeploymentConfig>>();
                    var deviceClientprovider = c.Resolve<IModuleClientProvider>();
                    var requestManager = c.Resolve<IRequestManager>();
                    var deviceManager = c.Resolve<IDeviceManager>();
                    bool enableSubscriptions = !this.experimentalFeatures.DisableCloudSubscriptions;
                    var deploymentMetrics = c.Resolve<IDeploymentMetrics>();
                    IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(deviceClientprovider, serde, requestManager, deviceManager, enableSubscriptions, this.configRefreshFrequency, deploymentMetrics);
                    return edgeAgentConnection;
                })
                .As<IEdgeAgentConnection>()
                .SingleInstance();

            // Task<IStreamRequestListener>
            builder.Register(
                    async c =>
                    {
                        if (this.enableStreams)
                        {
                            var runtimeInfoProviderTask = c.Resolve<Task<IRuntimeInfoProvider>>();
                            var logsProviderTask = c.Resolve<Task<ILogsProvider>>();
                            var edgeAgentConnection = c.Resolve<IEdgeAgentConnection>();
                            IRuntimeInfoProvider runtimeInfoProvider = await runtimeInfoProviderTask;
                            ILogsProvider logsProvider = await logsProviderTask;
                            var streamRequestHandlerProvider = new StreamRequestHandlerProvider(logsProvider, runtimeInfoProvider);
                            return new StreamRequestListener(streamRequestHandlerProvider, edgeAgentConnection) as IStreamRequestListener;
                        }
                        else
                        {
                            return new NullStreamRequestListener() as IStreamRequestListener;
                        }
                    })
                .As<Task<IStreamRequestListener>>()
                .SingleInstance();

            // Task<IConfigSource>
            builder.Register(
                async c =>
                {
                    var edgeAgentConnection = c.Resolve<IEdgeAgentConnection>();
                    var twinConfigSource = new TwinConfigSource(edgeAgentConnection, this.configuration);
                    var backupSourceTask = c.Resolve<Task<IDeploymentBackupSource>>();
                    IConfigSource backupConfigSource = new BackupConfigSource(await backupSourceTask, twinConfigSource);
                    return backupConfigSource;
                })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            // IReporter
            builder.Register(
                c =>
                {
                    var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
                    {
                        [DockerType] = typeof(DockerReportedRuntimeInfo),
                        [Constants.Unknown] = typeof(UnknownRuntimeInfo)
                    };

                    var edgeAgentDeserializerTypes = new Dictionary<string, Type>
                    {
                        [DockerType] = typeof(EdgeAgentDockerRuntimeModule),
                        [Constants.Unknown] = typeof(UnknownEdgeAgentModule)
                    };

                    var edgeHubDeserializerTypes = new Dictionary<string, Type>
                    {
                        [DockerType] = typeof(EdgeHubDockerRuntimeModule),
                        [Constants.Unknown] = typeof(UnknownEdgeHubModule)
                    };

                    var moduleDeserializerTypes = new Dictionary<string, Type>
                    {
                        [DockerType] = typeof(DockerRuntimeModule)
                    };

                    var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
                    {
                        { typeof(IRuntimeInfo), runtimeInfoDeserializerTypes },
                        { typeof(IEdgeAgentModule), edgeAgentDeserializerTypes },
                        { typeof(IEdgeHubModule), edgeHubDeserializerTypes },
                        { typeof(IModule), moduleDeserializerTypes }
                    };

                    var edgeAgentConnection = c.Resolve<IEdgeAgentConnection>();
                    return new IoTHubReporter(
                        edgeAgentConnection,
                        new TypeSpecificSerDe<AgentState>(deserializerTypesMap),
                        this.versionInfo) as IReporter;
                })
                .As<IReporter>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
