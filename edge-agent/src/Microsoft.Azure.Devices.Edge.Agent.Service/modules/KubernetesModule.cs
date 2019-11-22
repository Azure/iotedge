// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using k8s;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.DeviceManager;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Deployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Pvc;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.ServiceAccount;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Planners;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;
    using Constants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class KubernetesModule : Module
    {
        static readonly TimeSpan SystemInfoTimeout = TimeSpan.FromSeconds(3);
        readonly ResourceName resourceName;
        readonly string edgeDeviceHostName;
        readonly string proxyImage;
        readonly Option<string> proxyImagePullSecretName;
        readonly string proxyConfigPath;
        readonly string proxyConfigVolumeName;
        readonly string proxyConfigMapName;
        readonly string proxyTrustBundlePath;
        readonly string proxyTrustBundleVolumeName;
        readonly string proxyTrustBundleConfigMapName;
        readonly string apiVersion;
        readonly string deviceNamespace;
        readonly Uri managementUri;
        readonly Uri workloadUri;
        readonly IEnumerable<global::Docker.DotNet.Models.AuthConfig> dockerAuthConfig;
        readonly Option<UpstreamProtocol> upstreamProtocol;
        readonly Option<string> productInfo;
        readonly PortMapServiceType defaultMapServiceType;
        readonly bool enableServiceCallTracing;
        readonly string persistentVolumeName;
        readonly string storageClassName;
        readonly Option<uint> persistentVolumeClaimSizeMb;
        readonly Option<IWebProxy> proxy;
        readonly bool closeOnIdleTimeout;
        readonly TimeSpan idleTimeout;
        readonly KubernetesExperimentalFeatures experimentalFeatures;
        readonly KubernetesModuleOwner moduleOwner;
        readonly bool runAsNonRoot;

        public KubernetesModule(
            string iotHubHostname,
            string deviceId,
            string edgeDeviceHostName,
            string proxyImage,
            Option<string> proxyImagePullSecretName,
            string proxyConfigPath,
            string proxyConfigVolumeName,
            string proxyConfigMapName,
            string proxyTrustBundlePath,
            string proxyTrustBundleVolumeName,
            string proxyTrustBundleConfigMapName,
            string apiVersion,
            string deviceNamespace,
            Uri managementUri,
            Uri workloadUri,
            IEnumerable<global::Docker.DotNet.Models.AuthConfig> dockerAuthConfig,
            Option<UpstreamProtocol> upstreamProtocol,
            Option<string> productInfo,
            PortMapServiceType defaultMapServiceType,
            bool enableServiceCallTracing,
            string persistentVolumeName,
            string storageClassName,
            Option<uint> persistentVolumeClaimSizeMb,
            Option<IWebProxy> proxy,
            bool closeOnIdleTimeout,
            TimeSpan idleTimeout,
            KubernetesExperimentalFeatures experimentalFeatures,
            KubernetesModuleOwner moduleOwner,
            bool runAsNonRoot)
        {
            this.resourceName = new ResourceName(iotHubHostname, deviceId);
            this.edgeDeviceHostName = Preconditions.CheckNonWhiteSpace(edgeDeviceHostName, nameof(edgeDeviceHostName));
            this.proxyImage = Preconditions.CheckNonWhiteSpace(proxyImage, nameof(proxyImage));
            this.proxyImagePullSecretName = proxyImagePullSecretName;
            this.proxyConfigPath = Preconditions.CheckNonWhiteSpace(proxyConfigPath, nameof(proxyConfigPath));
            this.proxyConfigVolumeName = Preconditions.CheckNonWhiteSpace(proxyConfigVolumeName, nameof(proxyConfigVolumeName));
            this.proxyConfigMapName = Preconditions.CheckNonWhiteSpace(proxyConfigMapName, nameof(proxyConfigMapName));
            this.proxyTrustBundlePath = Preconditions.CheckNonWhiteSpace(proxyTrustBundlePath, nameof(proxyTrustBundlePath));
            this.proxyTrustBundleVolumeName = Preconditions.CheckNonWhiteSpace(proxyTrustBundleVolumeName, nameof(proxyTrustBundleVolumeName));
            this.proxyTrustBundleConfigMapName = Preconditions.CheckNonWhiteSpace(proxyTrustBundleConfigMapName, nameof(proxyTrustBundleConfigMapName));
            this.apiVersion = Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));
            this.deviceNamespace = Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace));
            this.managementUri = Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            this.workloadUri = Preconditions.CheckNotNull(workloadUri, nameof(workloadUri));
            this.dockerAuthConfig = Preconditions.CheckNotNull(dockerAuthConfig, nameof(dockerAuthConfig));
            this.upstreamProtocol = Preconditions.CheckNotNull(upstreamProtocol, nameof(upstreamProtocol));
            this.productInfo = productInfo;
            this.defaultMapServiceType = defaultMapServiceType;
            this.enableServiceCallTracing = enableServiceCallTracing;
            this.persistentVolumeName = persistentVolumeName;
            this.storageClassName = storageClassName;
            this.persistentVolumeClaimSizeMb = persistentVolumeClaimSizeMb;
            this.proxy = proxy;
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            this.idleTimeout = idleTimeout;
            this.experimentalFeatures = experimentalFeatures;
            this.moduleOwner = moduleOwner;
            this.runAsNonRoot = runAsNonRoot;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IKubernetesClient
            builder.Register(
                    c =>
                    {
                        if (this.enableServiceCallTracing)
                        {
                            // enable tracing of k8s requests made by the client
                            var loggerFactory = c.Resolve<ILoggerFactory>();
                            ILogger logger = loggerFactory.CreateLogger(typeof(Kubernetes));
                            ServiceClientTracing.IsEnabled = true;
                            ServiceClientTracing.AddTracingInterceptor(new DebugTracer(logger));
                        }

                        // load the k8s config from KUBECONFIG or $HOME/.kube/config or in-cluster if its available
                        KubernetesClientConfiguration kubeConfig = Option.Maybe(Environment.GetEnvironmentVariable("KUBECONFIG"))
                            .Else(() => Option.Maybe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kube", "config")))
                            .Filter(File.Exists)
                            .Map(path => KubernetesClientConfiguration.BuildConfigFromConfigFile(path))
                            .GetOrElse(KubernetesClientConfiguration.InClusterConfig);

                        return new Kubernetes(kubeConfig);
                    })
                .As<IKubernetes>()
                .SingleInstance();

            // IModuleClientProvider
            builder.Register(
                    c => new ModuleClientProvider(
                        c.Resolve<ISdkModuleClientProvider>(),
                        this.upstreamProtocol,
                        this.proxy,
                        this.productInfo.OrDefault(),
                        this.closeOnIdleTimeout,
                        this.idleTimeout))
                .As<IModuleClientProvider>()
                .SingleInstance();

            // IModuleManager
            builder.Register(c => new ModuleManagementHttpClient(this.managementUri, this.apiVersion, Core.Constants.EdgeletClientApiVersion))
                .As<IModuleManager>()
                .As<IIdentityManager>()
                .As<IDeviceManager>()
                .SingleInstance();

            // IModuleIdentityLifecycleManager
            var identityBuilder = new ModuleIdentityProviderServiceBuilder(this.resourceName.Hostname, this.resourceName.DeviceId, this.edgeDeviceHostName);
            builder.Register(c => new KubernetesModuleIdentityLifecycleManager(c.Resolve<IIdentityManager>(), identityBuilder, this.workloadUri))
                .As<IModuleIdentityLifecycleManager>()
                .SingleInstance();

            // CombinedKubernetesConfigProvider
            builder.Register(
                    c =>
                    {
                        bool enableKubernetesExtensions = this.experimentalFeatures.Enabled && this.experimentalFeatures.EnableExtensions;
                        return new CombinedKubernetesConfigProvider(this.dockerAuthConfig, this.workloadUri, this.managementUri, enableKubernetesExtensions);
                    })
                .As<ICombinedConfigProvider<CombinedKubernetesConfig>>()
                .SingleInstance();

            // ICommandFactory
            builder.Register(
                    c =>
                    {
                        var metricsProvider = c.Resolve<IMetricsProvider>();
                        var loggerFactory = c.Resolve<ILoggerFactory>();
                        ICommandFactory factory = new KubernetesCommandFactory();
                        factory = new MetricsCommandFactory(factory, metricsProvider);
                        factory = new LoggingCommandFactory(factory, loggerFactory);
                        return Task.FromResult(factory);
                    })
                .As<Task<ICommandFactory>>()
                .SingleInstance();

            // IPlanner
            builder.Register(
                    async c =>
                    {
                        var configProvider = c.Resolve<ICombinedConfigProvider<CombinedKubernetesConfig>>();
                        ICommandFactory commandFactory = await c.Resolve<Task<ICommandFactory>>();
                        IPlanner planner = new KubernetesPlanner(
                                    this.deviceNamespace,
                                    this.resourceName,
                                    c.Resolve<IKubernetes>(),
                                    commandFactory,
                                    configProvider,
                                    this.moduleOwner);
                        return planner;
                    })
                .As<Task<IPlanner>>()
                .SingleInstance();

            // KubernetesRuntimeInfoProvider
            builder.Register(c => new KubernetesRuntimeInfoProvider(this.deviceNamespace, c.Resolve<IKubernetes>(), c.Resolve<IModuleManager>()))
                .As<IRuntimeInfoProvider>()
                .As<IRuntimeInfoSource>()
                .SingleInstance();

            // KubernetesDeploymentProvider
            builder.Register(
                    c => new KubernetesDeploymentMapper(
                            this.deviceNamespace,
                            this.edgeDeviceHostName,
                            this.proxyImage,
                            this.proxyImagePullSecretName,
                            this.proxyConfigPath,
                            this.proxyConfigVolumeName,
                            this.proxyConfigMapName,
                            this.proxyTrustBundlePath,
                            this.proxyTrustBundleVolumeName,
                            this.proxyTrustBundleConfigMapName,
                            this.defaultMapServiceType,
                            this.persistentVolumeName,
                            this.storageClassName,
                            this.persistentVolumeClaimSizeMb,
                            this.apiVersion,
                            this.workloadUri,
                            this.managementUri,
                            this.runAsNonRoot,
                            this.enableServiceCallTracing,
                            this.experimentalFeatures.GetEnvVars()))
                .As<IKubernetesDeploymentMapper>();

            // KubernetesServiceMapper
            builder.Register(c => new KubernetesServiceMapper(this.defaultMapServiceType))
                .As<IKubernetesServiceMapper>();

            // KubernetesPvcMapper
            builder.Register(c => new KubernetesPvcMapper(this.persistentVolumeName, this.storageClassName, this.persistentVolumeClaimSizeMb.OrDefault()))
                .As<IKubernetesPvcMapper>();

            // KubernetesServiceAccountProvider
            builder.Register(c => new KubernetesServiceAccountMapper())
                .As<IKubernetesServiceAccountMapper>();

            // EdgeDeploymentController
            builder.Register(
                    c =>
                    {
                        var deploymentSelector = $"{Constants.K8sEdgeDeviceLabel}={KubeUtils.SanitizeK8sValue(this.resourceName.DeviceId)},{Constants.K8sEdgeHubNameLabel}={KubeUtils.SanitizeK8sValue(this.resourceName.Hostname)}";
                        IEdgeDeploymentController watchOperator = new EdgeDeploymentController(
                            this.resourceName,
                            deploymentSelector,
                            this.deviceNamespace,
                            c.Resolve<IKubernetes>(),
                            c.Resolve<IModuleIdentityLifecycleManager>(),
                            c.Resolve<IKubernetesServiceMapper>(),
                            c.Resolve<IKubernetesDeploymentMapper>(),
                            c.Resolve<IKubernetesPvcMapper>(),
                            c.Resolve<IKubernetesServiceAccountMapper>());

                        return watchOperator;
                    })
                .As<IEdgeDeploymentController>()
                .SingleInstance();

            // IEdgeDeploymentOperator
            builder.Register(
                    c =>
                    {
                        IEdgeDeploymentOperator watchOperator = new EdgeDeploymentOperator(
                            this.resourceName,
                            this.deviceNamespace,
                            c.Resolve<IKubernetes>(),
                            c.Resolve<IEdgeDeploymentController>());

                        return watchOperator;
                    })
                .As<IEdgeDeploymentOperator>()
                .SingleInstance();

            // IKubernetesEnvironmentOperator
            builder.Register(
                    c =>
                    {
                        IKubernetesEnvironmentOperator watchOperator = new KubernetesEnvironmentOperator(
                            this.deviceNamespace,
                            c.Resolve<IRuntimeInfoSource>(),
                            c.Resolve<IKubernetes>());

                        return watchOperator;
                    })
                .As<IKubernetesEnvironmentOperator>()
                .SingleInstance();

            // Task<IEnvironmentProvider>
            builder.Register(
                    async c =>
                    {
                        CancellationTokenSource tokenSource = new CancellationTokenSource(SystemInfoTimeout);
                        var moduleStateStore = await c.Resolve<Task<IEntityStore<string, ModuleState>>>();
                        var restartPolicyManager = c.Resolve<IRestartPolicyManager>();
                        IRuntimeInfoProvider runtimeInfoProvider = c.Resolve<IRuntimeInfoProvider>();
                        IEnvironmentProvider dockerEnvironmentProvider = await DockerEnvironmentProvider.CreateAsync(runtimeInfoProvider, moduleStateStore, restartPolicyManager, tokenSource.Token);
                        return dockerEnvironmentProvider;
                    })
                .As<Task<IEnvironmentProvider>>()
                .SingleInstance();
        }
    }

    class DebugTracer : IServiceClientTracingInterceptor
    {
        readonly ILogger logger;

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
