// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Commands
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
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;
    using Newtonsoft.Json;

    using Constants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class KubernetesCrdCommand<T> : ICommand
    {
        readonly IKubernetes client;
        readonly KubernetesModule<DockerConfig>[] modules;
        readonly Option<IRuntimeInfo> runtimeInfo;
        readonly Lazy<string> id;
        readonly ICombinedConfigProvider<T> combinedConfigProvider;
        readonly string deviceNamespace;
        readonly string iotHubHostname;
        readonly string deviceId;
        readonly TypeSpecificSerDe<EdgeDeploymentDefinition<DockerConfig>> deploymentSerde;
        // We use the sum of the IDs of the underlying commands as the id for this group
        // command.
        public string Id => this.id.Value;

        public KubernetesCrdCommand(string deviceNamespace, string iotHubHostname, string deviceId, IKubernetes client, KubernetesModule<DockerConfig>[] modules, Option<IRuntimeInfo> runtimeInfo, ICombinedConfigProvider<T> combinedConfigProvider)
        {
            this.deviceNamespace = KubeUtils.SanitizeK8sValue(Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace)));
            this.iotHubHostname = KubeUtils.SanitizeK8sValue(Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname)));
            this.deviceId = KubeUtils.SanitizeK8sValue(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.modules = Preconditions.CheckNotNull(modules, nameof(modules));
            this.runtimeInfo = Preconditions.CheckNotNull(runtimeInfo, nameof(runtimeInfo));
            this.combinedConfigProvider = Preconditions.CheckNotNull(combinedConfigProvider, nameof(combinedConfigProvider));
            this.id = new Lazy<string>(() => this.modules.Aggregate(string.Empty, (prev, module) => module.Name + prev));
            var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
            {
                [typeof(IModule)] = new Dictionary<string, Type>
                {
                    ["docker"] = typeof(CombinedDockerConfig)
                }
            };

            this.deploymentSerde = new TypeSpecificSerDe<EdgeDeploymentDefinition<DockerConfig>>(deserializerTypesMap);
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
                            if ((s.Data != null) && s.Data.TryGetValue(Constants.K8sPullSecretData, out byte[] pullSecretData) &&
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
            string resourceName = this.iotHubHostname + Constants.K8sNameDivider + this.deviceId;
            string metaApiVersion = Constants.K8sApi + "/" + Constants.K8sApiVersion;

            var modulesList = new List<KubernetesModule<DockerConfig>>();
            var secrets = new Dictionary<string, ImagePullSecret>();
            foreach (var runtime in this.runtimeInfo)
            {
                foreach (var m in this.modules)
                {
                    var combinedConfig = this.combinedConfigProvider.GetCombinedConfig(m, runtime);
                    CombinedDockerConfig dockerConfig = combinedConfig as CombinedDockerConfig;
                    if (dockerConfig != null)
                    {
                        var combinedModule = new KubernetesModule<DockerConfig>(m)
                        {
                            Config = new DockerConfig(dockerConfig.Image, dockerConfig.CreateOptions)
                        };
                        modulesList.Add(combinedModule);
                        dockerConfig.AuthConfig.ForEach(
                            auth =>
                            {
                                var kubernetesAuth = new ImagePullSecret(auth);
                                secrets[kubernetesAuth.Name] = kubernetesAuth;
                            });
                    }
                    else
                    {
                        throw new InvalidModuleException("Cannot convert combined config into KubernetesModule.");
                    }
                }
            }

            Option<EdgeDeploymentDefinition<DockerConfig>> activeDeployment;
            try
            {
                HttpOperationResponse<object> currentDeployment = await this.client.GetNamespacedCustomObjectWithHttpMessagesAsync(
                    Constants.K8sCrdGroup,
                    Constants.K8sApiVersion,
                    this.deviceNamespace,
                    Constants.K8sCrdPlural,
                    resourceName,
                    cancellationToken: token);
                string body = JsonConvert.SerializeObject(currentDeployment.Body);

                activeDeployment = currentDeployment.Response.IsSuccessStatusCode ?
                    Option.Some(this.deploymentSerde.Deserialize(body)) :
                    Option.None<EdgeDeploymentDefinition<DockerConfig>>();
            }
            catch (Exception parseException)
            {
                Events.FindActiveDeploymentFailed(resourceName, parseException);
                activeDeployment = Option.None<EdgeDeploymentDefinition<DockerConfig>>();
            }

            await this.UpdateImagePullSecrets(secrets, token);

            var metadata = new V1ObjectMeta(name: resourceName, namespaceProperty: this.deviceNamespace);
            // need resourceVersion for Replace.
            activeDeployment.ForEach(deployment => metadata.ResourceVersion = deployment.Metadata.ResourceVersion);
            var customObjectDefinition = new EdgeDeploymentDefinition<DockerConfig>(metaApiVersion, Constants.K8sCrdKind, metadata, modulesList);
            string customObjectString = this.deploymentSerde.Serialize(customObjectDefinition);

            // the dotnet client is apparently really picky about all names being camelCase,
            object crdObject = JsonConvert.DeserializeObject(customObjectString);

            await activeDeployment.Match(
                async a =>
                {
                    Events.ReplaceDeployment(customObjectString);
                    await this.client.ReplaceNamespacedCustomObjectWithHttpMessagesAsync(
                        crdObject,
                        Constants.K8sCrdGroup,
                        Constants.K8sApiVersion,
                        this.deviceNamespace,
                        Constants.K8sCrdPlural,
                        resourceName,
                        cancellationToken: token);
                },
                async () =>
                {
                    Events.CreateDeployment(customObjectString);
                    await this.client.CreateNamespacedCustomObjectWithHttpMessagesAsync(
                        crdObject,
                        Constants.K8sCrdGroup,
                        Constants.K8sApiVersion,
                        this.deviceNamespace,
                        Constants.K8sCrdPlural,
                        cancellationToken: token);
                });
        }

        public Task UndoAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public string Show()
        {
            IEnumerable<string> commandDescriptions = this.modules.Select(m => $"[{m.Name}]");
            return $"Create a CRD with modules: (\n  {string.Join("\n  ", commandDescriptions)}\n)";
        }

        public override string ToString() => this.Show();

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesCommand;
            static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesCrdCommand<T>>();

            enum EventIds
            {
                CreateDeployment = IdStart,
                FailedToFindSecret,
                SecretCreateUpdateFailed,
                FindActiveDeploymentFailed,
                ReplaceDeployment
            }

            public static void CreateDeployment(string customObjectString)
            {
                Log.LogDebug(
                    (int)EventIds.CreateDeployment,
                    "===================CREATE========================\n" +
                    customObjectString +
                    "\n=================================================");
            }

            public static void FailedToFindSecret(string key, Exception exception)
            {
                Log.LogDebug((int)EventIds.FailedToFindSecret, exception, $"Failed to find image pull secret ${key}");
            }

            public static void SecretCreateUpdateFailed(string key, Exception exception)
            {
                Log.LogError((int)EventIds.SecretCreateUpdateFailed, exception, $"Failed to create or update image pull secret ${key}");
            }

            public static void FindActiveDeploymentFailed(string resourceName, Exception parseException)
            {
                Log.LogDebug((int)EventIds.FindActiveDeploymentFailed, parseException, $"Failed to find active edge deployment ${resourceName}");
            }

            public static void ReplaceDeployment(string customObjectString)
            {
                Log.LogDebug(
                    (int)EventIds.ReplaceDeployment,
                    "====================REPLACE======================\n" +
                    customObjectString +
                    "\n=================================================");
            }
        }
    }
}
