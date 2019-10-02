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
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
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
        readonly ICombinedConfigProvider<CombinedDockerConfig> combinedConfigProvider;
        readonly string deviceNamespace;
        readonly ResourceName resourceName;
        readonly JsonSerializerSettings serializerSettings;

        // We use the sum of the IDs of the underlying commands as the id for this group
        // command.
        public string Id => this.id.Value;

        public EdgeDeploymentCommand(
            string deviceNamespace,
            ResourceName resourceName,
            IKubernetes client,
            IEnumerable<IModule> modules,
            IRuntimeInfo runtimeInfo,
            ICombinedConfigProvider<CombinedDockerConfig> combinedConfigProvider)
        {
            this.deviceNamespace = KubeUtils.SanitizeK8sValue(Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace)));
            this.resourceName = Preconditions.CheckNotNull(resourceName, nameof(resourceName));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.modules = Preconditions.CheckNotNull(modules, nameof(modules)).ToList();
            this.runtimeInfo = Preconditions.CheckNotNull(runtimeInfo, nameof(runtimeInfo));
            this.combinedConfigProvider = Preconditions.CheckNotNull(combinedConfigProvider, nameof(combinedConfigProvider));
            this.id = new Lazy<string>(() => this.modules.Aggregate(string.Empty, (prev, module) => module.Name + prev));
            this.serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new OverrideJsonIgnoreOfBaseClassContractResolver(
                    new Dictionary<Type, string[]>
                    {
                        [typeof(KubernetesModule)] = new[] { nameof(KubernetesModule.Name) }
                    }),
            };
        }

        async Task UpdateImagePullSecrets(Dictionary<string, ImagePullSecret> imagePullSecrets, CancellationToken token)
        {
            foreach (KeyValuePair<string, ImagePullSecret> imagePullSecret in imagePullSecrets)
            {
                var secretData = new Dictionary<string, byte[]> { [Constants.K8sPullSecretData] = Encoding.UTF8.GetBytes(imagePullSecret.Value.GenerateSecret()) };
                var secretMeta = new V1ObjectMeta(name: imagePullSecret.Key, namespaceProperty: this.deviceNamespace);
                var newSecret = new V1Secret("v1", secretData, type: Constants.K8sPullSecretType, kind: "Secret", metadata: secretMeta);
                Option<V1Secret> currentSecret;
                try
                {
                    currentSecret = Option.Maybe(await this.client.ReadNamespacedSecretAsync(imagePullSecret.Key, this.deviceNamespace, cancellationToken: token));
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    Events.FailedToFindSecret(imagePullSecret.Key, ex);
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
                                imagePullSecret.Key,
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
                    Events.SecretCreateUpdateFailed(imagePullSecret.Key, ex);
                }
            }
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            List<KubernetesModule> modulesList = this.modules.Select(
                    module =>
                    {
                        var config = this.combinedConfigProvider.GetCombinedConfig(module, this.runtimeInfo);
                        return new KubernetesModule(module, config);
                    })
                .ToList();

            // Modules may share an image pull secret, so only pick unique ones to add to the dictionary.
            Dictionary<string, ImagePullSecret> secrets = modulesList
                .Select(module => module.Config.AuthConfig.Map(auth => new ImagePullSecret(auth)).OrDefault())
                .Where(secret => secret != null)
                .GroupBy(secret => secret.Name)
                .Select(secretGroup => secretGroup.First())
                .ToDictionary(secret => secret.Name);

            Option<EdgeDeploymentDefinition> activeDeployment;
            try
            {
                JObject currentDeployment = await this.client.GetNamespacedCustomObjectAsync(
                    Constants.EdgeDeployment.Group,
                    Constants.EdgeDeployment.Version,
                    this.deviceNamespace,
                    Constants.EdgeDeployment.Plural,
                    this.resourceName,
                    token) as JObject;

                activeDeployment = Option.Maybe(currentDeployment)
                    .Map(deployment => deployment.ToObject<EdgeDeploymentDefinition>(JsonSerializer.Create(this.serializerSettings)));
            }
            catch (Exception parseException)
            {
                Events.FindActiveDeploymentFailed(this.resourceName, parseException);
                activeDeployment = Option.None<EdgeDeploymentDefinition>();
            }

            await this.UpdateImagePullSecrets(secrets, token);

            var metadata = new V1ObjectMeta(name: this.resourceName, namespaceProperty: this.deviceNamespace);

            // need resourceVersion for Replace.
            activeDeployment.ForEach(deployment => metadata.ResourceVersion = deployment.Metadata.ResourceVersion);

            var customObjectDefinition = new EdgeDeploymentDefinition(Constants.EdgeDeployment.ApiVersion, Constants.EdgeDeployment.Kind, metadata, modulesList);
            var crdObject = JObject.FromObject(customObjectDefinition, JsonSerializer.Create(this.serializerSettings));

            await activeDeployment.Match(
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

        public Task UndoAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

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

            public static void CreateEdgeDeployment(EdgeDeploymentDefinition deployment)
            {
                Log.LogDebug((int)EventIds.CreateDeployment, $"Create edge deployment: {deployment.Metadata.Name}");
            }

            public static void FailedToFindSecret(string key, Exception exception)
            {
                Log.LogDebug((int)EventIds.FailedToFindSecret, exception, $"Failed to find image pull secret ${key}");
            }

            public static void SecretCreateUpdateFailed(string key, Exception exception)
            {
                Log.LogError((int)EventIds.SecretCreateUpdateFailed, exception, $"Failed to create or update image pull secret ${key}");
            }

            public static void FindActiveDeploymentFailed(ResourceName resourceName, Exception parseException)
            {
                Log.LogDebug((int)EventIds.FindActiveDeploymentFailed, parseException, $"Failed to find active edge deployment ${resourceName}");
            }

            public static void ReplaceEdgeDeployment(EdgeDeploymentDefinition deployment)
            {
                Log.LogDebug((int)EventIds.ReplaceDeployment, $"Replace edge deployment: {deployment.Metadata.Name}");
            }
        }
    }
}
