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
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;
    using Constants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class KubernetesCrdCommand<T> : ICommand
    {
        readonly IKubernetes client;
        readonly KubernetesModule[] modules;
        readonly Option<IRuntimeInfo> runtimeInfo;
        readonly Lazy<string> id;
        readonly ICombinedConfigProvider<T> combinedConfigProvider;
        readonly string iotHubHostname;
        readonly string deviceId;
        readonly TypeSpecificSerDe<EdgeDeploymentDefinition> deploymentSerde;
        readonly JsonSerializerSettings jsonSettings;

        public KubernetesCrdCommand(string iotHubHostname, string deviceId, IKubernetes client, KubernetesModule[] modules, Option<IRuntimeInfo> runtimeInfo, ICombinedConfigProvider<T> combinedConfigProvider)
        {
            this.iotHubHostname = KubeUtils.SanitizeK8sValue(Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname)));
            this.deviceId = KubeUtils.SanitizeK8sValue(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.modules = Preconditions.CheckNotNull(modules, nameof(modules));
            this.runtimeInfo = Preconditions.CheckNotNull(runtimeInfo, nameof(runtimeInfo));
            this.combinedConfigProvider = Preconditions.CheckNotNull(combinedConfigProvider, nameof(combinedConfigProvider));
            this.id = new Lazy<string>(() => this.modules.Aggregate("", (prev, module) => module.ModuleIdentity.ModuleId + prev));
            var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
            {
                [typeof(IModule)] = new Dictionary<string, Type>
                {
                    ["docker"] = typeof(CombinedDockerModule)
                }
            };

            this.deploymentSerde = new TypeSpecificSerDe<EdgeDeploymentDefinition>(deserializerTypesMap, new CamelCasePropertyNamesContractResolver());
            this.jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        // We use the sum of the IDs of the underlying commands as the id for this group
        // command.
        public string Id => this.id.Value;

        async Task UpdateImagePullSecrets(Dictionary<string, ImagePullSecret> imagePullSecrets, CancellationToken token)
        {
            foreach (KeyValuePair<string, ImagePullSecret> imagePullSecret in imagePullSecrets)
            {
                var secretData = new Dictionary<string, byte[]>();
                secretData[Constants.k8sPullSecretData] = Encoding.UTF8.GetBytes(imagePullSecret.Value.GenerateSecret());
                var secretMeta = new V1ObjectMeta(name: imagePullSecret.Key, namespaceProperty: Constants.k8sNamespace);
                var newSecret = new V1Secret("v1", secretData, type: Constants.k8sPullSecretType, kind: "Secret", metadata: secretMeta);
                Option<V1Secret> currentSecret;
                try
                {
                    currentSecret = Option.Maybe(await this.client.ReadNamespacedSecretAsync(imagePullSecret.Key, Constants.k8sNamespace, cancellationToken: token));
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    Events.FailedToFindSecret(imagePullSecret.Key, ex);
                    currentSecret = Option.None<V1Secret>();
                }

                try
                {
                    await currentSecret.Match(
                        async s =>
                        {
                            if (!s.Data[Constants.k8sPullSecretData].SequenceEqual(secretData[Constants.k8sPullSecretData]))
                            {
                                return await this.client.ReplaceNamespacedSecretAsync(
                                    newSecret,
                                    imagePullSecret.Key,
                                    Constants.k8sNamespace,
                                    cancellationToken: token);
                            }

                            return s;
                        },
                        async () => await this.client.CreateNamespacedSecretAsync(newSecret, Constants.k8sNamespace, cancellationToken: token));
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    Events.SecretCreateUpdateFailed(imagePullSecret.Key, ex);
                    Console.WriteLine(ex.Message);
                }
            }

        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            string resourceName = this.iotHubHostname + Constants.k8sNameDivider + this.deviceId;
            string metaApiVersion = Constants.k8sApi + "/" + Constants.k8sApiVersion;

            var modulesList = new List<KubernetesModule>();
            var secrets = new Dictionary<string, ImagePullSecret>();
            foreach (var runtime in this.runtimeInfo)
            {
                foreach (var m in this.modules)
                {
                    var combinedConfig = this.combinedConfigProvider.GetCombinedConfig(m.Module, runtime);
                    var combinedModule = new CombinedDockerModule(
                        m.Module.Name,
                        m.Module.Version,
                        m.Module.DesiredStatus,
                        m.Module.RestartPolicy,
                        combinedConfig as CombinedDockerConfig,
                        m.Module.ConfigurationInfo,
                        m.Module.Env);
                    var combinedIdentity = new KubernetesModule(combinedModule, m.ModuleIdentity);
                    combinedModule.Config.AuthConfig.ForEach(
                        auth =>
                        {
                            var kubernetesAuth = new ImagePullSecret(auth);
                            secrets[kubernetesAuth.Name] = kubernetesAuth;
                        });
                    modulesList.Add(combinedIdentity);
                }
            }

            //TODO: Validate Spec here?

            Option<EdgeDeploymentDefinition> activeDeployment;
            try
            {
                HttpOperationResponse<object> currentDeployment = await this.client.GetNamespacedCustomObjectWithHttpMessagesAsync(
                    Constants.k8sCrdGroup,
                    Constants.k8sApiVersion,
                    Constants.k8sNamespace,
                    Constants.k8sCrdPlural,
                    resourceName,
                    cancellationToken: token);
                string body = JsonConvert.SerializeObject(currentDeployment.Body);
                Console.WriteLine("=================================================");
                Console.WriteLine(body);
                Console.WriteLine("=================================================");
                activeDeployment = currentDeployment.Response.IsSuccessStatusCode ?
                    Option.Some(this.deploymentSerde.Deserialize(body)) :
                    Option.None<EdgeDeploymentDefinition>();
            }
            catch (Exception parseException)
            {
                Events.FindActiveDeploymentFailed(resourceName, parseException);
                Console.WriteLine(parseException.Message);
                activeDeployment = Option.None<EdgeDeploymentDefinition>();
            }

            await this.UpdateImagePullSecrets(secrets, token);

            var metadata = new V1ObjectMeta(name: resourceName, namespaceProperty: Constants.k8sNamespace);
            // need resourceVersion for Replace.
            activeDeployment.ForEach(deployment => metadata.ResourceVersion = deployment.Metadata.ResourceVersion);
            var customObjectDefinition = new EdgeDeploymentDefinition(metaApiVersion, Constants.k8sCrdKind, metadata, modulesList);
            string customObjectString = this.deploymentSerde.Serialize(customObjectDefinition);

            // the dotnet client is apparently really picky about all names being camelCase
            object crdObject = JsonConvert.DeserializeObject(customObjectString);
            //object crdObject = customObjectDefinition;

            if (!activeDeployment.HasValue)
            {
                Events.CreateDeployment(customObjectString);
                await this.client.CreateNamespacedCustomObjectWithHttpMessagesAsync(
                    crdObject,
                    Constants.k8sCrdGroup,
                    Constants.k8sApiVersion,
                    Constants.k8sNamespace,
                    Constants.k8sCrdPlural,
                    cancellationToken: token);

            }
            else
            {
                Events.ReplaceDeployment(customObjectString);
                await this.client.ReplaceNamespacedCustomObjectWithHttpMessagesAsync(
                    crdObject,
                    Constants.k8sCrdGroup,
                    Constants.k8sApiVersion,
                    Constants.k8sNamespace,
                    Constants.k8sCrdPlural,
                    resourceName,
                    cancellationToken: token);
            }
        }

        public Task UndoAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public string Show()
        {
            IEnumerable<string> commandDescriptions = this.modules.Select(m => $"[{m.Module.Name}]");
            return $"Create a CRD with modules: (\n  {string.Join("\n  ", commandDescriptions)}\n)";
        }

        public override string ToString() => this.Show();

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesCrdCommand<T>>();
            const int IdStart = AgentEventIds.KubernetesCommand;

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
                Log.LogDebug((int)EventIds.CreateDeployment,
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
                Log.LogDebug((int)EventIds.ReplaceDeployment,
                    "====================REPLACE======================\n" +
                    customObjectString +
                    "\n=================================================");
            }
        }

    }
}
