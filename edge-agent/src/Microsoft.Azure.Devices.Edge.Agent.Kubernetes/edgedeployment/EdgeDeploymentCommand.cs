// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Diff;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class EdgeDeploymentCommand : ICommand
    {
        readonly IKubernetes client;
        readonly IReadOnlyCollection<IModule> modules;
        readonly IRuntimeInfo runtimeInfo;
        readonly Lazy<string> id;
        readonly ICombinedConfigProvider<CombinedKubernetesConfig> configProvider;
        readonly string deviceSelector;
        readonly string deviceNamespace;
        readonly ResourceName resourceName;
        readonly JsonSerializerSettings serializerSettings;
        readonly KubernetesModuleOwner moduleOwner;
        readonly Option<EdgeDeploymentDefinition> activeDeployment;

        // We use the sum of the IDs of the underlying commands as the id for this group
        // command.
        public string Id => this.id.Value;

        public EdgeDeploymentCommand(
            ResourceName resourceName,
            string deviceSelector,
            string deviceNamespace,
            IKubernetes client,
            IEnumerable<IModule> desiredModules,
            Option<EdgeDeploymentDefinition> activeDeployment,
            IRuntimeInfo runtimeInfo,
            ICombinedConfigProvider<CombinedKubernetesConfig> configProvider,
            KubernetesModuleOwner moduleOwner)
        {
            this.resourceName = Preconditions.CheckNotNull(resourceName, nameof(resourceName));
            this.deviceNamespace = KubeUtils.SanitizeK8sValue(Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace)));
            this.deviceSelector = Preconditions.CheckNonWhiteSpace(deviceSelector, nameof(deviceSelector));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.modules = Preconditions.CheckNotNull(desiredModules, nameof(desiredModules)).ToList();
            this.activeDeployment = activeDeployment;
            this.runtimeInfo = Preconditions.CheckNotNull(runtimeInfo, nameof(runtimeInfo));
            this.configProvider = Preconditions.CheckNotNull(configProvider, nameof(configProvider));
            this.id = new Lazy<string>(() => this.modules.Aggregate(string.Empty, (prev, module) => module.Name + prev));
            this.serializerSettings = EdgeDeploymentSerialization.SerializerSettings;
            this.moduleOwner = Preconditions.CheckNotNull(moduleOwner, nameof(moduleOwner));
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await this.ManageImagePullSecrets(token);
            await this.PushEdgeDeployment(token);
        }

        async Task ManageImagePullSecrets(CancellationToken token)
        {
            var deviceOnlyLabels = new Dictionary<string, string>
            {
                [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue(this.resourceName.DeviceId),
            };

            // Modules may share an image pull secret, so only pick unique ones to add to the dictionary.
            List<V1Secret> desiredImagePullSecrets = this.modules
                .Select(module => this.configProvider.GetCombinedConfig(module, this.runtimeInfo))
                .Select(config => config.ImagePullSecret)
                .FilterMap()
                .GroupBy(secret => secret.Name)
                .Select(secretGroup => this.CreateSecret(secretGroup.First(), deviceOnlyLabels))
                .ToList();

            V1SecretList currentImagePullSecrets = await this.client.ListNamespacedSecretAsync(this.deviceNamespace, labelSelector: this.deviceSelector, cancellationToken: token);

            await this.ManageImagePullSecrets(currentImagePullSecrets, desiredImagePullSecrets, token);
        }

        V1Secret CreateSecret(ImagePullSecret imagePullSecret, IDictionary<string, string> labels) =>
            new V1Secret
            {
                Data = new Dictionary<string, byte[]>
                {
                    [KubernetesConstants.K8sPullSecretData] = Encoding.UTF8.GetBytes(imagePullSecret.GenerateSecret())
                },
                Type = KubernetesConstants.K8sPullSecretType,
                Metadata = new V1ObjectMeta
                {
                    Name = imagePullSecret.Name,
                    NamespaceProperty = this.deviceNamespace,
                    OwnerReferences = this.moduleOwner.ToOwnerReferences(),
                    Labels = labels
                }
            };

        async Task ManageImagePullSecrets(V1SecretList existing, List<V1Secret> desired, CancellationToken token)
        {
            // find difference between desired and existing image pull secrets
            var diff = FindImagePullSecretDiff(desired, existing.Items);

            // Update only those image pull secrets if configurations have not matched
            var updatingTask = diff.Updated
                .Select(
                    update =>
                    {
                        Events.UpdateImagePullSecret(update.To);

                        update.To.Metadata.ResourceVersion = update.From.Metadata.ResourceVersion;
                        return this.client.ReplaceNamespacedSecretAsync(update.To, update.To.Metadata.Name, this.deviceNamespace, cancellationToken: token);
                    });
            await Task.WhenAll(updatingTask);

            // Delete all existing image pull secrets that are not in desired list
            var removingTasks = diff.Removed
                .Select(
                    name =>
                    {
                        Events.DeleteImagePullSecret(name);
                        return this.client.DeleteNamespacedSecretAsync(name, this.deviceNamespace, cancellationToken: token);
                    });
            await Task.WhenAll(removingTasks);

            // Create new desired image pull secrets
            foreach (V1Secret secret in diff.Added)
            {
                // Allow user to override image pull secrets even if they were created not by agent (e.g. during installation and/or iotedged)
                try
                {
                    Events.CreateImagePullSecret(secret);
                    await this.client.CreateNamespacedSecretAsync(secret, this.deviceNamespace, cancellationToken: token);
                }
                catch (HttpOperationException e) when (e.Response.StatusCode == HttpStatusCode.Conflict)
                {
                    Events.UpdateExistingImagePullSecret(secret);

                    V1Secret conflictedSecret = await this.client.ReadNamespacedSecretAsync(secret.Metadata.Name, secret.Metadata.NamespaceProperty, cancellationToken: token);
                    conflictedSecret.Data = secret.Data;

                    await this.client.ReplaceNamespacedSecretAsync(conflictedSecret, conflictedSecret.Metadata.Name, conflictedSecret.Metadata.NamespaceProperty, cancellationToken: token);
                }
            }
        }

        static Diff<V1Secret> FindImagePullSecretDiff(IEnumerable<V1Secret> desired, IEnumerable<V1Secret> existing)
        {
            var desiredSet = new Set<V1Secret>(desired.ToDictionary(secret => secret.Metadata.Name));
            var existingSet = new Set<V1Secret>(existing.ToDictionary(secret => secret.Metadata.Name));

            return desiredSet.Diff(existingSet, ImagePullSecretBySecretDataEqualityComparer);
        }

        static IEqualityComparer<V1Secret> ImagePullSecretBySecretDataEqualityComparer { get; } = new KubernetesImagePullSecretBySecretDataEqualityComparer();

        async Task PushEdgeDeployment(CancellationToken token)
        {
            List<KubernetesModule> modulesList = this.modules
                .Select(
                    module =>
                    {
                        var combinedConfig = this.configProvider.GetCombinedConfig(module, this.runtimeInfo);
                        string image = combinedConfig.Image;

                        var authConfig = combinedConfig.ImagePullSecret.Map(secret => new AuthConfig(secret.Name));
                        return new KubernetesModule(module, new KubernetesConfig(image, combinedConfig.CreateOptions, authConfig), this.moduleOwner);
                    })
                .ToList();

            var metadata = new V1ObjectMeta
            {
                Name = this.resourceName,
                NamespaceProperty = this.deviceNamespace,
                OwnerReferences = this.moduleOwner.ToOwnerReferences()
            };

            // need resourceVersion for Replace.
            this.activeDeployment.ForEach(deployment => metadata.ResourceVersion = deployment.Metadata.ResourceVersion);

            var customObjectDefinition = new EdgeDeploymentDefinition(KubernetesConstants.EdgeDeployment.ApiVersion, KubernetesConstants.EdgeDeployment.Kind, metadata, modulesList);
            var crdObject = JObject.FromObject(customObjectDefinition, JsonSerializer.Create(this.serializerSettings));

            await this.activeDeployment.Match(
                async a =>
                {
                    Events.ReplaceEdgeDeployment(customObjectDefinition);
                    await this.client.ReplaceNamespacedCustomObjectWithHttpMessagesAsync(
                        crdObject,
                        KubernetesConstants.EdgeDeployment.Group,
                        KubernetesConstants.EdgeDeployment.Version,
                        this.deviceNamespace,
                        KubernetesConstants.EdgeDeployment.Plural,
                        this.resourceName,
                        cancellationToken: token);
                },
                async () =>
                {
                    try
                    {
                        Events.CreateEdgeDeployment(customObjectDefinition);
                        await this.client.CreateNamespacedCustomObjectWithHttpMessagesAsync(
                            crdObject,
                            KubernetesConstants.EdgeDeployment.Group,
                            KubernetesConstants.EdgeDeployment.Version,
                            this.deviceNamespace,
                            KubernetesConstants.EdgeDeployment.Plural,
                            cancellationToken: token);
                    }
                    catch (HttpOperationException e) when (e.Response.StatusCode == HttpStatusCode.NotFound)
                    {
                        Events.ReportCrdInstallationFailed(e);
                        throw;
                    }
                });
        }

        public Task UndoAsync(CancellationToken token) => Task.CompletedTask;

        public string Show() => $"Create an EdgeDeployment with modules: [{string.Join(", ", this.modules.Select(m => m.Name))}]";

        public override string ToString() => this.Show();

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesCommand;
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeDeploymentCommand>();

            enum EventIds
            {
                CreateDeployment = IdStart,
                ReplaceDeployment,
                CreateImagePullSecret,
                DeleteImagePullSecret,
                UpdateImagePullSecret,
                UpdateExistingImagePullSecret,
                ReportCrdInstallationFailed
            }

            public static void CreateEdgeDeployment(EdgeDeploymentDefinition deployment) => Log.LogDebug((int)EventIds.CreateDeployment, $"Create edge deployment: {deployment.Metadata.Name}");

            public static void ReplaceEdgeDeployment(EdgeDeploymentDefinition deployment) => Log.LogDebug((int)EventIds.ReplaceDeployment, $"Replace edge deployment: {deployment.Metadata.Name}");

            internal static void CreateImagePullSecret(V1Secret secret) => Log.LogDebug((int)EventIds.CreateImagePullSecret, $"Create Image Pull Secret {secret.Metadata.Name}");

            internal static void DeleteImagePullSecret(string name) => Log.LogDebug((int)EventIds.DeleteImagePullSecret, $"Delete Image Pull Secret {name}");

            internal static void UpdateImagePullSecret(V1Secret secret) => Log.LogDebug((int)EventIds.UpdateImagePullSecret, $"Update Image Pull Secret {secret.Metadata.Name}");

            internal static void UpdateExistingImagePullSecret(V1Secret secret) => Log.LogWarning((int)EventIds.UpdateExistingImagePullSecret, $"Update existing Image Pull Secret {secret.Metadata.Name}");

            internal static void ReportCrdInstallationFailed(Exception ex) => Log.LogError((int)EventIds.ReportCrdInstallationFailed, "EdgeDeployment CustomResourceDefinition(CRD) was not found. Please install the edge-kubernetes-crd Helm chart");
        }
    }
}
