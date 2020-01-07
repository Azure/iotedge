// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Constants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class EdgeDeploymentCommand : ICommand
    {
        readonly IKubernetes client;
        readonly IReadOnlyCollection<IModule> modules;
        readonly IRuntimeInfo runtimeInfo;
        readonly Lazy<string> id;
        readonly ICombinedConfigProvider<CombinedKubernetesConfig> configProvider;
        readonly string deviceNamespace;
        readonly ResourceName resourceName;
        readonly JsonSerializerSettings serializerSettings;
        readonly KubernetesModuleOwner moduleOwner;
        readonly Option<EdgeDeploymentDefinition> activeDeployment;

        // We use the sum of the IDs of the underlying commands as the id for this group
        // command.
        public string Id => this.id.Value;

        public EdgeDeploymentCommand(
            string deviceNamespace,
            ResourceName resourceName,
            IKubernetes client,
            IEnumerable<IModule> desiredmodules,
            Option<EdgeDeploymentDefinition> activeDeployment,
            IRuntimeInfo runtimeInfo,
            ICombinedConfigProvider<CombinedKubernetesConfig> configProvider,
            KubernetesModuleOwner moduleOwner)
        {
            this.deviceNamespace = KubeUtils.SanitizeK8sValue(Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace)));
            this.resourceName = Preconditions.CheckNotNull(resourceName, nameof(resourceName));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.modules = Preconditions.CheckNotNull(desiredmodules, nameof(desiredmodules)).ToList();
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
            // Modules may share an image pull secret, so only pick unique ones to add to the dictionary.
            List<ImagePullSecret> secrets = this.modules
                .Select(module => this.configProvider.GetCombinedConfig(module, this.runtimeInfo))
                .Select(config => config.ImagePullSecret)
                .FilterMap()
                .GroupBy(secret => secret.Name)
                .Select(secretGroup => secretGroup.First())
                .ToList();

            await this.UpdateImagePullSecrets(secrets, token);
        }

        async Task UpdateImagePullSecrets(IEnumerable<ImagePullSecret> imagePullSecrets, CancellationToken token)
        {
            foreach (var imagePullSecret in imagePullSecrets)
            {
                var secretMeta = new V1ObjectMeta(
                    name: imagePullSecret.Name,
                    namespaceProperty: this.deviceNamespace,
                    ownerReferences: this.moduleOwner.ToOwnerReferences());
                var secretData = new Dictionary<string, byte[]> { [Constants.K8sPullSecretData] = Encoding.UTF8.GetBytes(imagePullSecret.GenerateSecret()) };
                var newSecret = new V1Secret("v1", secretData, type: Constants.K8sPullSecretType, kind: "Secret", metadata: secretMeta);
                Option<V1Secret> currentSecret;
                try
                {
                    currentSecret = Option.Maybe(await this.client.ReadNamespacedSecretAsync(imagePullSecret.Name, this.deviceNamespace, cancellationToken: token));
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    Events.FailedToFindSecret(imagePullSecret.Name, ex);
                    currentSecret = Option.None<V1Secret>();
                }

                try
                {
                    var v1Secret = await currentSecret.Match(
                        async s =>
                        {
                            if (s.Data != null && s.Data.TryGetValue(Constants.K8sPullSecretData, out byte[] pullSecretData) &&
                                pullSecretData.SequenceEqual(secretData[Constants.K8sPullSecretData]))
                            {
                                return s;
                            }

                            return await this.client.ReplaceNamespacedSecretAsync(
                                newSecret,
                                imagePullSecret.Name,
                                this.deviceNamespace,
                                cancellationToken: token);
                        },
                        async () => await this.client.CreateNamespacedSecretAsync(newSecret, this.deviceNamespace, cancellationToken: token));
                    if (v1Secret == null)
                    {
                        throw new InvalidIdentityException("Image pull secret was not properly created");
                    }
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    Events.SecretCreateUpdateFailed(imagePullSecret.Name, ex);
                }
            }
        }

        async Task PushEdgeDeployment(CancellationToken token)
        {
            List<KubernetesModule> modulesList = this.modules
                .Select(
                    module =>
                    {
                        var combinedConfig = this.configProvider.GetCombinedConfig(module, this.runtimeInfo);
                        string image = combinedConfig.Image;

                        // TODO: this is a workaround in preview to keep Edge Agent from updating itself
                        if (module.Name == Core.Constants.EdgeAgentModuleName)
                        {
                            var agentImage = this.FindAgentImageAsync(token).ConfigureAwait(false);
                            agentImage.GetAwaiter().GetResult().ForEach(foundImage => image = foundImage);
                        }

                        var authConfig = combinedConfig.ImagePullSecret.Map(secret => new AuthConfig(secret.Name));
                        return new KubernetesModule(module, new KubernetesConfig(image, combinedConfig.CreateOptions, authConfig), this.moduleOwner);
                    })
                .ToList();

            var metadata = new V1ObjectMeta(
                name: this.resourceName,
                namespaceProperty: this.deviceNamespace,
                ownerReferences: this.moduleOwner.ToOwnerReferences());

            // need resourceVersion for Replace.
            this.activeDeployment.ForEach(deployment => metadata.ResourceVersion = deployment.Metadata.ResourceVersion);

            var customObjectDefinition = new EdgeDeploymentDefinition(Constants.EdgeDeployment.ApiVersion, Constants.EdgeDeployment.Kind, metadata, modulesList);
            var crdObject = JObject.FromObject(customObjectDefinition, JsonSerializer.Create(this.serializerSettings));

            await this.activeDeployment.Match(
                async a =>
                {
                    Events.ReplaceEdgeDeployment(customObjectDefinition);
                    await this.client.ReplaceNamespacedCustomObjectWithHttpMessagesAsync(
                        crdObject,
                        Constants.EdgeDeployment.Group,
                        Constants.EdgeDeployment.Version,
                        this.deviceNamespace,
                        Constants.EdgeDeployment.Plural,
                        this.resourceName,
                        cancellationToken: token);
                },
                async () =>
                {
                    Events.CreateEdgeDeployment(customObjectDefinition);
                    await this.client.CreateNamespacedCustomObjectWithHttpMessagesAsync(
                        crdObject,
                        Constants.EdgeDeployment.Group,
                        Constants.EdgeDeployment.Version,
                        this.deviceNamespace,
                        Constants.EdgeDeployment.Plural,
                        cancellationToken: token);
                });
        }

        Task<Option<string>> FindAgentImageAsync(CancellationToken token)
        {
            var agentImage = this.activeDeployment.Match(
                edgeDeployment =>
                {
                    var currentAgent = this.activeDeployment.OrDefault().Spec.First(agentModule => agentModule.Name == Core.Constants.EdgeAgentModuleName);
                    return Task.FromResult(Option.Some(currentAgent.Config.Image));
                },
                async () =>
                {
                    try
                    {
                        // When CRD has not been created, use helm chart deployment details
                        var agentDeployment = await this.client.ReadNamespacedDeploymentAsync(
                                Core.Constants.EdgeAgentModuleName.ToLower(),
                                this.deviceNamespace,
                                cancellationToken: token);
                        return Option.Some(agentDeployment.Spec.Template.Spec.Containers.First(container => container.Name == Core.Constants.EdgeAgentModuleName.ToLower()).Image);
                    }
                    catch (Exception e)
                    {
                        Events.FindActiveDeploymentFailed(Core.Constants.EdgeAgentModuleName, e);
                        return Option.None<string>();
                    }
                });

            return agentImage;
        }

        public Task UndoAsync(CancellationToken token) => Task.CompletedTask;

        public string Show() => $"Create an EdgeDeployment with modules: ({string.Join(", ", this.modules.Select(m => m.Name))}\n)";

        public override string ToString() => this.Show();

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesCommand;
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeDeploymentCommand>();

            enum EventIds
            {
                CreateDeployment = IdStart,
                FailedToFindSecret,
                SecretCreateUpdateFailed,
                FindActiveDeploymentFailed,
                ReplaceDeployment
            }

            public static void CreateEdgeDeployment(EdgeDeploymentDefinition deployment) => Log.LogDebug((int)EventIds.CreateDeployment, $"Create edge deployment: {deployment.Metadata.Name}");

            public static void FailedToFindSecret(string key, Exception exception) => Log.LogDebug((int)EventIds.FailedToFindSecret, exception, $"Failed to find image pull secret ${key}");

            public static void SecretCreateUpdateFailed(string key, Exception exception) => Log.LogError((int)EventIds.SecretCreateUpdateFailed, exception, $"Failed to create or update image pull secret ${key}");

            public static void FindActiveDeploymentFailed(string deploymentName, Exception exception) => Log.LogDebug((int)EventIds.FindActiveDeploymentFailed, exception, $"Failed to find active edge deployment ${deploymentName}");

            public static void ReplaceEdgeDeployment(EdgeDeploymentDefinition deployment) => Log.LogDebug((int)EventIds.ReplaceDeployment, $"Replace edge deployment: {deployment.Metadata.Name}");
        }
    }
}
