// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet.Models;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Rest;
    using Moq;
    using Moq.Language.Flow;
    using Newtonsoft.Json.Linq;
    using Xunit;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class EdgeDeploymentCommandTest
    {
        const string Selector = "selector";
        const string Namespace = "namespace";
        static readonly ResourceName ResourceName = new ResourceName("hostname", "deviceId");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly AuthConfig DockerAuth = new AuthConfig { Username = "username", Password = "password", ServerAddress = "docker.io" };
        static readonly ImagePullSecret ImagePullSecret = new ImagePullSecret(DockerAuth);
        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");
        static readonly DockerConfig Config2 = new DockerConfig("test-image:2");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly KubernetesModuleOwner EdgeletModuleOwner = new KubernetesModuleOwner("v1", "Deployment", "iotedged", "123");
        static readonly IKubernetes DefaultClient = Mock.Of<IKubernetes>();
        static readonly ICombinedConfigProvider<CombinedKubernetesConfig> ConfigProvider = Mock.Of<ICombinedConfigProvider<CombinedKubernetesConfig>>();
        static readonly IRuntimeInfo Runtime = Mock.Of<IRuntimeInfo>();

        [Fact]
        public void Constructor_ThrowsException_OnInvalidParams()
        {
            KubernetesConfig config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<global::Microsoft.Azure.Devices.Edge.Agent.Kubernetes.AuthConfig>());
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVars);
            KubernetesModule km1 = new KubernetesModule(m1, config, EdgeletModuleOwner);
            KubernetesModule[] modules = { km1 };
            EdgeDeploymentDefinition edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>());
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(null, Selector, Namespace, DefaultClient, modules, Option.Maybe(edgeDefinition), Runtime, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentException>(() => new EdgeDeploymentCommand(ResourceName, null, Namespace, DefaultClient, modules, Option.Maybe(edgeDefinition), Runtime, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentException>(() => new EdgeDeploymentCommand(ResourceName, Selector, null, DefaultClient, modules, Option.Maybe(edgeDefinition), Runtime, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(ResourceName, Selector, Namespace, null, modules, Option.Maybe(edgeDefinition), Runtime, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(ResourceName, Selector, Namespace, DefaultClient, null, Option.Maybe(edgeDefinition), Runtime, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(ResourceName, Selector, Namespace, DefaultClient, modules, Option.Maybe(edgeDefinition), null, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(ResourceName, Selector, Namespace, DefaultClient, modules, Option.Maybe(edgeDefinition), Runtime, null, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(ResourceName, Selector, Namespace, DefaultClient, modules, Option.Maybe(edgeDefinition), Runtime, ConfigProvider, null));
        }

        [Fact]
        public async void Execute_CreatesNewImagePullSecret_WhenEmpty()
        {
            IModule dockerModule = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVars);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.None<string>(), Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));

            var client = new Mock<IKubernetes>();
            client.SetupListSecrets()
                .ReturnsAsync(
                    () =>
                        new HttpOperationResponse<V1SecretList>
                        {
                            Response = new HttpResponseMessage(),
                            Body = new V1SecretList { Items = new List<V1Secret>() }
                        });
            V1Secret createdSecret = null;
            client.SetupCreateSecret()
                .Callback(
                    (V1Secret body, string ns, string dryRun, string fieldManager, string pretty, Dictionary<string, List<string>> customHeaders, CancellationToken token) => { createdSecret = body; })
                .ReturnsAsync(() => CreateResponse(HttpStatusCode.Created, new V1Secret()));
            client.SetupCreateEdgeDeploymentDefinition().ReturnsAsync(() => CreateResponse(HttpStatusCode.Created, new object()));
            var cmd = new EdgeDeploymentCommand(ResourceName, Selector, Namespace, client.Object, new[] { dockerModule }, Option.None<EdgeDeploymentDefinition>(), Runtime, configProvider.Object, EdgeletModuleOwner);

            await cmd.ExecuteAsync(CancellationToken.None);

            Assert.NotNull(createdSecret);
            client.VerifyAll();
        }

        [Fact]
        public async void Execute_UpdatesImagePullSecret_WhenExistsWithSameName()
        {
            string secretName = "username-docker.io";
            var secretData = new Dictionary<string, byte[]> { [KubernetesConstants.K8sPullSecretData] = Encoding.UTF8.GetBytes("Invalid Secret Data") };
            var secretMeta = new V1ObjectMeta(name: secretName, namespaceProperty: Namespace);
            var existingSecret = new V1Secret("v1", secretData, type: KubernetesConstants.K8sPullSecretType, kind: "Secret", metadata: secretMeta);
            IModule dockerModule = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVars);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.None<string>(), Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));

            var client = new Mock<IKubernetes>();
            client.SetupListSecrets().ReturnsAsync(() => CreateResponse(new V1SecretList { Items = new List<V1Secret> { existingSecret } }));
            V1Secret updatedSecret = null;
            client.SetupUpdateSecret()
                .Callback(
                    (V1Secret body, string name, string ns, string dryRun, string fieldManager, string pretty, Dictionary<string, List<string>> customHeaders, CancellationToken token) => { updatedSecret = body; })
                .ReturnsAsync(() => CreateResponse(new V1Secret()));
            client.SetupCreateEdgeDeploymentDefinition().ReturnsAsync(() => CreateResponse(HttpStatusCode.Created, new object()));
            var cmd = new EdgeDeploymentCommand(ResourceName, Selector, Namespace, client.Object, new[] { dockerModule }, Option.None<EdgeDeploymentDefinition>(), Runtime, configProvider.Object, EdgeletModuleOwner);

            await cmd.ExecuteAsync(CancellationToken.None);

            Assert.NotNull(updatedSecret);
            client.VerifyAll();
        }

        [Fact]
        public async void Execute_UpdatesEdgeDeploymentDefinition_WhenExistsWithSameName()
        {
            IModule dockerModule = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVars);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.None<string>(), Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));
            var existingDeployment = new EdgeDeploymentDefinition(KubernetesConstants.EdgeDeployment.ApiVersion, KubernetesConstants.EdgeDeployment.Kind, new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>());

            var client = new Mock<IKubernetes>();
            client.SetupListSecrets().ReturnsAsync(() => CreateResponse(new V1SecretList { Items = new List<V1Secret>() }));
            client.SetupCreateSecret().ReturnsAsync(() => CreateResponse(HttpStatusCode.Created, new V1Secret()));
            client.SetupUpdateEdgeDeploymentDefinition().ReturnsAsync(CreateResponse(HttpStatusCode.Created, new object()));
            var cmd = new EdgeDeploymentCommand(ResourceName, Selector, Namespace, client.Object, new[] { dockerModule }, Option.Some(existingDeployment), Runtime, configProvider.Object, EdgeletModuleOwner);

            await cmd.ExecuteAsync(CancellationToken.None);

            client.VerifyAll();
        }

        [Fact]
        public async void Execute_CreatesOnlyOneImagePullSecret_When2ModulesWithSameSecret()
        {
            IModule dockerModule1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVars);
            IModule dockerModule2 = new DockerModule("module2", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config2, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVars);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(It.IsAny<DockerModule>(), Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.None<string>(), Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(It.IsAny<DockerModule>(), Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));

            var client = new Mock<IKubernetes>();
            client.SetupListSecrets().ReturnsAsync(() => CreateResponse(new V1SecretList { Items = new List<V1Secret>() }));
            client.SetupCreateSecret().ReturnsAsync(() => CreateResponse(HttpStatusCode.Created, new V1Secret()));
            client.SetupCreateEdgeDeploymentDefinition().ReturnsAsync(CreateResponse(HttpStatusCode.Created, new object()));
            var cmd = new EdgeDeploymentCommand(ResourceName, Selector, Namespace, client.Object, new[] { dockerModule1, dockerModule2 }, Option.None<EdgeDeploymentDefinition>(), Runtime, configProvider.Object, EdgeletModuleOwner);

            await cmd.ExecuteAsync(CancellationToken.None);

            client.VerifyAll();
            client.VerifyCreateSecret(Times.Once());
        }

        [Fact]
        public async void Execute_PreservesCaseOfEnvVars_WhenModuleDeployed()
        {
            IDictionary<string, EnvVal> moduleEnvVars = new Dictionary<string, EnvVal> { { "ACamelCaseEnvVar", new EnvVal("ACamelCaseEnvVarValue") } };
            IModule dockerModule = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, moduleEnvVars);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.None<string>(), Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));

            var client = new Mock<IKubernetes>();
            client.SetupListSecrets().ReturnsAsync(() => CreateResponse(new V1SecretList { Items = new List<V1Secret>() }));
            client.SetupCreateSecret().ReturnsAsync(() => CreateResponse(HttpStatusCode.Created, new V1Secret()));

            EdgeDeploymentDefinition edgeDeploymentDefinition = null;
            client.SetupCreateEdgeDeploymentDefinition()
                .Callback(
                    (object body, string group, string version, string ns, string plural, string name, Dictionary<string, List<string>> headers, CancellationToken token) => { edgeDeploymentDefinition = ((JObject)body).ToObject<EdgeDeploymentDefinition>(); })
                .ReturnsAsync(() => CreateResponse<object>(edgeDeploymentDefinition));

            var cmd = new EdgeDeploymentCommand(ResourceName, Selector, Namespace, client.Object, new[] { dockerModule }, Option.None<EdgeDeploymentDefinition>(), Runtime, configProvider.Object, EdgeletModuleOwner);

            await cmd.ExecuteAsync(CancellationToken.None);

            Assert.Equal("module1", edgeDeploymentDefinition.Spec[0].Name);
            Assert.Equal("test-image:1", edgeDeploymentDefinition.Spec[0].Config.Image);
            Assert.True(edgeDeploymentDefinition.Spec[0].Env.Contains(new KeyValuePair<string, EnvVal>("ACamelCaseEnvVar", new EnvVal("ACamelCaseEnvVarValue"))));
        }

        [Fact]
        public async void Execute_UpdatesSecretData_WhenImagePullSecretCreatedNotByAgent()
        {
            string secretName = "username-docker.io";
            var existingSecret = new V1Secret
            {
                Data = new Dictionary<string, byte[]> { [KubernetesConstants.K8sPullSecretData] = Encoding.UTF8.GetBytes("Invalid Secret Data") },
                Type = KubernetesConstants.K8sPullSecretType,
                Metadata = new V1ObjectMeta { Name = secretName, NamespaceProperty = Namespace, ResourceVersion = "1" }
            };

            IModule dockerModule = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVars);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.None<string>(), Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));

            var client = new Mock<IKubernetes>();
            client.SetupListSecrets().ReturnsAsync(() => CreateResponse(new V1SecretList { Items = new List<V1Secret>() }));
            client.SetupCreateSecret().ThrowsAsync(new HttpOperationException { Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Conflict), "Conflict") });
            V1Secret updatedSecret = null;
            client.SetupUpdateSecret()
                .Callback(
                    (V1Secret body, string name, string ns, string dryRun, string fieldManager, string pretty, Dictionary<string, List<string>> customHeaders, CancellationToken token) => { updatedSecret = body; })
                .ReturnsAsync(() => CreateResponse(updatedSecret));
            client.SetupGetSecret(secretName).ReturnsAsync(() => CreateResponse(existingSecret));
            var cmd = new EdgeDeploymentCommand(ResourceName, Selector, Namespace, client.Object, new[] { dockerModule }, Option.None<EdgeDeploymentDefinition>(), Runtime, configProvider.Object, EdgeletModuleOwner);

            await cmd.ExecuteAsync(CancellationToken.None);

            Assert.True(Encoding.UTF8.GetBytes(ImagePullSecret.GenerateSecret()).SequenceEqual(updatedSecret.Data[KubernetesConstants.K8sPullSecretData]));
            Assert.Equal("1", updatedSecret.Metadata.ResourceVersion);
            client.VerifyAll();
        }

        static HttpOperationResponse<TResult> CreateResponse<TResult>(TResult value) =>
            CreateResponse(HttpStatusCode.OK, value);

        static HttpOperationResponse<TResult> CreateResponse<TResult>(HttpStatusCode statusCode, TResult value) =>
            new HttpOperationResponse<TResult>
            {
                Response = new HttpResponseMessage(statusCode),
                Body = value
            };
    }

    public static class IKubernetesEdgeDeploymentDefinitionMockExtensions
    {
        public static ISetup<IKubernetes, Task<HttpOperationResponse<object>>> SetupCreateEdgeDeploymentDefinition(this Mock<IKubernetes> client) =>
            client.Setup(
                c => c.CreateNamespacedCustomObjectWithHttpMessagesAsync(
                    It.IsAny<object>(),
                    KubernetesConstants.EdgeDeployment.Group,
                    KubernetesConstants.EdgeDeployment.Version,
                    It.IsAny<string>(),
                    KubernetesConstants.EdgeDeployment.Plural,
                    It.IsAny<string>(),
                    null,
                    It.IsAny<CancellationToken>()));

        public static ISetup<IKubernetes, Task<HttpOperationResponse<object>>> SetupUpdateEdgeDeploymentDefinition(this Mock<IKubernetes> client) =>
            client.Setup(
                c => c.ReplaceNamespacedCustomObjectWithHttpMessagesAsync(
                    It.IsAny<object>(),
                    KubernetesConstants.EdgeDeployment.Group,
                    KubernetesConstants.EdgeDeployment.Version,
                    It.IsAny<string>(),
                    KubernetesConstants.EdgeDeployment.Plural,
                    It.IsAny<string>(),
                    null,
                    It.IsAny<CancellationToken>()));
    }

    public static class IKubernetesSecretMockExtensions
    {
        public static ISetup<IKubernetes, Task<HttpOperationResponse<V1SecretList>>> SetupListSecrets(this Mock<IKubernetes> client) =>
            client.Setup(
                c => c.ListNamespacedSecretWithHttpMessagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()));

        public static ISetup<IKubernetes, Task<HttpOperationResponse<V1Secret>>> SetupCreateSecret(this Mock<IKubernetes> client) =>
            client.Setup(
                c => c.CreateNamespacedSecretWithHttpMessagesAsync(
                    It.IsAny<V1Secret>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()));

        public static void VerifyCreateSecret(this Mock<IKubernetes> client, Times times) =>
            client.Verify(
                c => c.CreateNamespacedSecretWithHttpMessagesAsync(
                    It.IsAny<V1Secret>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()),
                times);

        public static ISetup<IKubernetes, Task<HttpOperationResponse<V1Secret>>> SetupUpdateSecret(this Mock<IKubernetes> client) =>
            client.Setup(
                c => c.ReplaceNamespacedSecretWithHttpMessagesAsync(
                    It.IsAny<V1Secret>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()));

        public static ISetup<IKubernetes, Task<HttpOperationResponse<V1Secret>>> SetupGetSecret(this Mock<IKubernetes> client, string name) =>
            client.Setup(
                c => c.ReadNamespacedSecretWithHttpMessagesAsync(
                    name,
                    It.IsAny<string>(),
                    It.IsAny<bool?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()));
    }
}
