// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class CrdWatchOperator : IKubernetesOperator
    {
        readonly IKubernetes client;
        readonly string iotHubHostname;
        readonly string deviceId;
        readonly string edgeHostname;
        readonly string resourceName;
        readonly string deploymentSelector;
        readonly string proxyImage;
        readonly string proxyConfigPath;
        readonly string proxyConfigVolumeName;
        readonly string proxyConfigMapName;
        readonly string proxyTrustBundlePath;
        readonly string proxyTrustBundleVolumeName;
        readonly string proxyTrustBundleConfigMapName;
        readonly string k8sNamespace;
        readonly string workloadApiVersion;
        readonly Uri workloadUri;
        readonly Uri managementUri;
        readonly string defaultMapServiceType;
        readonly IModuleIdentityLifecycleManager moduleIdentityLifecycleManager;
        Option<Watcher<V1Pod>> podWatch;

        public CrdWatchOperator(
            string iotHubHostname,
            string deviceId,
            string edgeHostname,
            string proxyImage,
            string proxyConfigPath,
            string proxyConfigVolumeName,
            string proxyConfigMapName,
            string proxyTrustBundlePath,
            string proxyTrustBundleVolumeName,
            string proxyTrustBundleConfigMapName,
            string k8sNamespace,
            string workloadApiVersion,
            Uri workloadUri,
            Uri managementUri,
            PortMapServiceType defaultMapServiceType,
            IKubernetes client,
            IModuleIdentityLifecycleManager moduleIdentityLifecycleManager)
        {
            this.iotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.edgeHostname = Preconditions.CheckNonWhiteSpace(edgeHostname, nameof(edgeHostname));
            this.proxyImage = Preconditions.CheckNonWhiteSpace(proxyImage, nameof(proxyImage));
            this.proxyConfigPath = Preconditions.CheckNonWhiteSpace(proxyConfigPath, nameof(proxyConfigPath));
            this.proxyConfigVolumeName = Preconditions.CheckNonWhiteSpace(proxyConfigVolumeName, nameof(proxyConfigVolumeName));
            this.proxyConfigMapName = Preconditions.CheckNonWhiteSpace(proxyConfigMapName, nameof(proxyConfigMapName));
            this.proxyTrustBundlePath = Preconditions.CheckNonWhiteSpace(proxyTrustBundlePath, nameof(proxyTrustBundlePath));
            this.proxyTrustBundleVolumeName = Preconditions.CheckNonWhiteSpace(proxyTrustBundleVolumeName, nameof(proxyTrustBundleVolumeName));
            this.proxyTrustBundleConfigMapName = Preconditions.CheckNonWhiteSpace(proxyTrustBundleConfigMapName, nameof(proxyTrustBundleConfigMapName));
            this.k8sNamespace = Preconditions.CheckNonWhiteSpace(k8sNamespace, nameof(k8sNamespace));
            this.workloadApiVersion = Preconditions.CheckNonWhiteSpace(workloadApiVersion, nameof(workloadApiVersion));
            this.workloadUri = Preconditions.CheckNotNull(workloadUri, nameof(workloadUri));
            this.managementUri = Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            this.defaultMapServiceType = Preconditions.CheckNotNull(defaultMapServiceType, nameof(defaultMapServiceType)).ToString();
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.podWatch = Option.None<Watcher<V1Pod>>();
            this.resourceName = KubeUtils.SanitizeK8sValue(this.iotHubHostname) + Constants.K8sNameDivider + KubeUtils.SanitizeK8sValue(this.deviceId);
            this.deploymentSelector = Constants.K8sEdgeDeviceLabel + " = " + KubeUtils.SanitizeK8sValue(this.deviceId) + "," + Constants.K8sEdgeHubNameLabel + "=" + KubeUtils.SanitizeK8sValue(this.iotHubHostname);

            this.moduleIdentityLifecycleManager = moduleIdentityLifecycleManager;
        }

        public Task CloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this.podWatch.ForEach(watch => watch.Dispose());
        }

        public void Start()
        {
            // The following "List..." requests do not return until there is something to return, so if we "await" here,
            // there is a chance that one or both of these requests will block forever - we won't start creating these pods and CRDs
            // until we receive a deployment.
            // Considering setting up these watches is critical to the operation of EdgeAgent, throwing an exception and letting the process crash
            // is an acceptable fate if these tasks fail.

            // CRD watch
            var watcher = new CrdWatcher(
                this.iotHubHostname,
                this.deviceId,
                this.edgeHostname,
                this.proxyImage,
                this.proxyConfigPath,
                this.proxyConfigVolumeName,
                this.proxyConfigMapName,
                this.proxyTrustBundlePath,
                this.proxyTrustBundleVolumeName,
                this.proxyTrustBundleConfigMapName,
                this.resourceName,
                this.deploymentSelector,
                this.defaultMapServiceType,
                this.k8sNamespace,
                this.workloadApiVersion,
                this.workloadUri,
                this.managementUri,
                this.moduleIdentityLifecycleManager,
                this.client);
            this.client.ListNamespacedCustomObjectWithHttpMessagesAsync(Constants.K8sCrdGroup, Constants.K8sApiVersion, this.k8sNamespace, Constants.K8sCrdPlural, watch: true).ContinueWith(watcher.ListCrdComplete);
        }
    }
}
