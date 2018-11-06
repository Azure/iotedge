// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Autofac;
    using k8s;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Planners;
    using Microsoft.Rest;
    using static System.Environment;
    using ModuleIdentityLifecycleManager = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.ModuleIdentityLifecycleManager;

    public class KubernetesModule : Module
    {
        readonly string deviceId;
        readonly string iotHubHostname;
        readonly string gatewayHostname;
        readonly string proxyImage;
        readonly string proxyConfigPath;
        readonly string proxyConfigVolumeName;
        readonly string serviceAccountName;
        readonly Uri managementUri;
        readonly Uri workloadUri;
        readonly IEnumerable<AuthConfig> dockerAuthConfig;
        readonly Option<UpstreamProtocol> upstreamProtocol;
        readonly Option<string> productInfo;
        readonly bool enableServiceCallTracing;

        public KubernetesModule(
            string iotHubHostname,
            string gatewayHostName,
            string deviceId,
            string proxyImage,
            string proxyConfigPath,
            string proxyConfigVolumeName,
            string serviceAccountName,
            Uri managementUri,
            Uri workloadUri,
            IEnumerable<AuthConfig> dockerAuthConfig,
            Option<UpstreamProtocol> upstreamProtocol,
            Option<string> productInfo,
            bool enableServiceCallTracing)
        {
            this.iotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.gatewayHostname = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.proxyImage = Preconditions.CheckNonWhiteSpace(proxyImage, nameof(proxyImage));
            this.proxyConfigPath = Preconditions.CheckNonWhiteSpace(proxyConfigPath, nameof(proxyConfigPath));
            this.proxyConfigVolumeName = Preconditions.CheckNonWhiteSpace(proxyConfigVolumeName, nameof(proxyConfigVolumeName));
            this.serviceAccountName = Preconditions.CheckNonWhiteSpace(serviceAccountName, nameof(serviceAccountName));
            this.managementUri = Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            this.workloadUri = Preconditions.CheckNotNull(workloadUri, nameof(workloadUri));
            this.dockerAuthConfig = Preconditions.CheckNotNull(dockerAuthConfig, nameof(dockerAuthConfig));
            this.upstreamProtocol = Preconditions.CheckNotNull(upstreamProtocol, nameof(upstreamProtocol));
            this.productInfo = productInfo;
            this.enableServiceCallTracing = enableServiceCallTracing;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IKubernetesClient
            builder.Register(c =>
                {
                    if (this.enableServiceCallTracing)
                    {
                        // enable tracing of k8s requests made by the client
                        var loggerFactory = c.Resolve<ILoggerFactory>();
                        ILogger logger = loggerFactory.CreateLogger(typeof(Kubernetes));
                        ServiceClientTracing.IsEnabled = true;
                        ServiceClientTracing.AddTracingInterceptor(new DebugTracer(logger));
                    }

                    // load the k8s config from $HOME/.kube/config if its available
                    KubernetesClientConfiguration kubeConfig;
                    string kubeConfigPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.UserProfile), ".kube", "config");
                    if (File.Exists(kubeConfigPath))
                    {
                        kubeConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile();
                    }
                    else
                    {
                        kubeConfig = KubernetesClientConfiguration.InClusterConfig();
                    }

                    var client = new Kubernetes(kubeConfig);
                    return client;
                })
                .As<IKubernetes>()
                .SingleInstance();

            // IModuleClientProvider
            builder.Register(c => new EnvironmentModuleClientProvider(this.upstreamProtocol, this.productInfo))
                .As<IModuleClientProvider>()
                .SingleInstance();

            // IModuleManager
            builder.Register(c => new ModuleManagementHttpClient(this.managementUri))
                .As<IModuleManager>()
                .As<IIdentityManager>()
                .SingleInstance();

            // IModuleIdentityLifecycleManager
            var identityBuilder = new ModuleIdentityProviderServiceBuilder(this.iotHubHostname, this.deviceId, this.gatewayHostname);
            builder.Register(c => new ModuleIdentityLifecycleManager(c.Resolve<IIdentityManager>(), identityBuilder, this.workloadUri))
                .As<IModuleIdentityLifecycleManager>()
                .SingleInstance();

            // ICombinedConfigProvider<CombinedDockerConfig>
            builder.Register(
                    async c =>
                    {
                        IConfigSource configSource = await c.Resolve<Task<IConfigSource>>();
                        return new CombinedKubernetesConfigProvider(this.dockerAuthConfig, configSource) as ICombinedConfigProvider<CombinedDockerConfig>;
                    })
                .As<Task<ICombinedConfigProvider<CombinedDockerConfig>>>()
                .SingleInstance();

            // ICommandFactory
            builder.Register(
                    async c =>
                    {
                        var client = c.Resolve<IKubernetes>();
                        var configSourceTask = c.Resolve<Task<IConfigSource>>();
                        var combinedDockerConfigProviderTask = c.Resolve<Task<ICombinedConfigProvider<CombinedDockerConfig>>>();
                        IConfigSource configSource = await configSourceTask;
                        ICombinedConfigProvider<CombinedDockerConfig> combinedDockerConfigProvider = await combinedDockerConfigProviderTask;
                        var kubernetesCommandFactory = new KubernetesCommandFactory(this.iotHubHostname, this.gatewayHostname, this.deviceId, client, configSource, combinedDockerConfigProvider);
                        return new LoggingCommandFactory(kubernetesCommandFactory, c.Resolve<ILoggerFactory>()) as ICommandFactory;
                    })
                .As<Task<ICommandFactory>>()
                .SingleInstance();

            // IPlanner
            builder.Register(async c =>
                {
                    var commandFactoryTask = c.Resolve<Task<ICommandFactory>>();
                    var combinedConfigProviderTask = c.Resolve<Task<ICombinedConfigProvider<CombinedDockerConfig>>>();
                    ICommandFactory commandFactory = await commandFactoryTask;
                    ICombinedConfigProvider<CombinedDockerConfig> combinedConfigProvider = await combinedConfigProviderTask;
                    return new KubernetesPlanner<CombinedDockerConfig>(this.iotHubHostname, this.gatewayHostname, this.deviceId, c.Resolve<IKubernetes>(), commandFactory, combinedConfigProvider) as IPlanner;
                })
                .As<Task<IPlanner>>()
                .SingleInstance();

            // IRuntimeInfoProvider, IKubernetesOperator
            builder.Register(
                    c => Task.FromResult(new EdgeOperator(
                        this.iotHubHostname,
                        this.deviceId,
                        this.gatewayHostname,
                        this.proxyImage,
                        this.proxyConfigPath,
                        this.proxyConfigVolumeName,
                        this.serviceAccountName,
                        this.workloadUri,
                        this.managementUri,
                        c.Resolve<IKubernetes>()) as IRuntimeInfoProvider))
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

    class DebugTracer : IServiceClientTracingInterceptor
    {
        ILogger logger;

        public DebugTracer(ILogger logger)
        {
            this.logger = logger;
        }

        public void Information(string message)
        {
            this.logger.LogInformation(message);
        }

        public void TraceError(string invocationId, Exception exception)
        {
            this.logger.LogError("Exception in {0}: {1}", invocationId, exception);
        }

        public void ReceiveResponse(string invocationId, HttpResponseMessage response)
        {
            string requestAsString = response == null ? string.Empty : response.AsFormattedString();
            this.logger.LogInformation("invocationId: {0}\r\nresponse: {1}", invocationId, requestAsString);
        }

        public void SendRequest(string invocationId, HttpRequestMessage request)
        {
            string requestAsString = request == null ? string.Empty : request.AsFormattedString();
            this.logger.LogInformation("invocationId: {0}\r\nrequest: {1}", invocationId, requestAsString);
        }

        public void Configuration(string source, string name, string value)
        {
            this.logger.LogInformation("Configuration: source={0}, name={1}, value={2}", source, name, value);
        }

        public void EnterMethod(string invocationId, object instance, string method, IDictionary<string, object> parameters)
        {
            this.logger.LogInformation(
                "invocationId: {0}\r\ninstance: {1}\r\nmethod: {2}\r\nparameters: {3}",
                invocationId,
                instance,
                method,
                parameters.AsFormattedString());
        }

        public void ExitMethod(string invocationId, object returnValue)
        {
            string returnValueAsString = returnValue == null ? string.Empty : returnValue.ToString();
            this.logger.LogInformation(
                "Exit with invocation id {0}, the return value is {1}",
                invocationId,
                returnValueAsString);
        }
    }
}
