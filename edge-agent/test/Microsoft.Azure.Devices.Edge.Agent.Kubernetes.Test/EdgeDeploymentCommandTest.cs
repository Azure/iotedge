// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;
    using Constants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class EdgeDeploymentCommandTest
    {
        const string Namespace = "namespace";
        static readonly ResourceName ResourceName = new ResourceName("hostname", "deviceId");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly global::Docker.DotNet.Models.AuthConfig DockerAuth = new global::Docker.DotNet.Models.AuthConfig { Username = "username", Password = "password", ServerAddress = "docker.io" };
        static readonly ImagePullSecret ImagePullSecret = new ImagePullSecret(DockerAuth);
        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");
        static readonly DockerConfig Config2 = new DockerConfig("test-image:2");
        static readonly DockerConfig AgentConfig1 = new DockerConfig("agent:3");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly KubernetesModuleOwner EdgeletModuleOwner = new KubernetesModuleOwner("v1", "Deployment", "iotedged", "123");
        static readonly IKubernetes DefaultClient = Mock.Of<IKubernetes>();
        static readonly ICombinedConfigProvider<CombinedKubernetesConfig> ConfigProvider = Mock.Of<ICombinedConfigProvider<CombinedKubernetesConfig>>();
        static readonly IRuntimeInfo Runtime = Mock.Of<IRuntimeInfo>();

        [Fact]
        [Unit]
        public void ConstructorThrowsOnInvalidParams()
        {
            KubernetesConfig config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<AuthConfig>());
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            KubernetesModule km1 = new KubernetesModule(m1, config, EdgeletModuleOwner);
            KubernetesModule[] modules = { km1 };
            EdgeDeploymentDefinition edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);
            Assert.Throws<ArgumentException>(() => new EdgeDeploymentCommand(null, ResourceName, DefaultClient, modules, Option.Maybe(edgeDefinition), Runtime, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(Namespace, null, DefaultClient, modules, Option.Maybe(edgeDefinition), Runtime, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(Namespace, ResourceName, null, modules, Option.Maybe(edgeDefinition), Runtime, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(Namespace, ResourceName, DefaultClient, null, Option.Maybe(edgeDefinition), Runtime, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(Namespace, ResourceName, DefaultClient, modules, Option.Maybe(edgeDefinition), null, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(Namespace, ResourceName, DefaultClient, modules, Option.Maybe(edgeDefinition), Runtime, null, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(Namespace, ResourceName, DefaultClient, modules, Option.Maybe(edgeDefinition), Runtime, ConfigProvider, null));
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteWithAuthCreateNewObjects()
        {
            IModule dockerModule = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            ModuleSet currentModules = ModuleSet.Create(dockerModule);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));
            bool getSecretCalled = false;
            bool postSecretCalled = false;
            bool postCrdCalled = false;

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.StatusCode = 404;
                        if (pathStr.Contains($"api/v1/namespaces/{Namespace}/secrets"))
                        {
                            getSecretCalled = true;
                        }
                    }
                    else if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.StatusCode = 201;
                        httpContext.Response.Body = httpContext.Request.Body;
                        if (pathStr.Contains($"api/v1/namespaces/{Namespace}/secrets"))
                        {
                            postSecretCalled = true;
                        }
                        else if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}"))
                        {
                            postCrdCalled = true;
                        }
                    }

                    return Task.FromResult(false);
                }))
            {
                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var cmd = new EdgeDeploymentCommand(Namespace, ResourceName, client, new[] { dockerModule }, Option.None<EdgeDeploymentDefinition>(), Runtime, configProvider.Object, EdgeletModuleOwner);

                await cmd.ExecuteAsync(CancellationToken.None);

                Assert.True(getSecretCalled, nameof(getSecretCalled));
                Assert.True(postSecretCalled, nameof(postSecretCalled));
                Assert.True(postCrdCalled, nameof(postCrdCalled));
            }
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteDeploysModulesWithEnvVars()
        {
            IDictionary<string, EnvVal> moduleEnvVars = new Dictionary<string, EnvVal> { { "ACamelCaseEnvVar", new EnvVal("ACamelCaseEnvVarValue") } };
            IModule dockerModule = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, moduleEnvVars);
            ModuleSet currentModules = ModuleSet.Create(dockerModule);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));
            EdgeDeploymentDefinition postedEdgeDeploymentDefinition = null;
            bool postCrdCalled = false;

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: async httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.StatusCode = 404;
                    }
                    else if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.StatusCode = 201;
                        httpContext.Response.Body = httpContext.Request.Body;
                        if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}"))
                        {
                            postCrdCalled = true;
                            using (var reader = new StreamReader(httpContext.Response.Body))
                            {
                                string crdBody = await reader.ReadToEndAsync();
                                postedEdgeDeploymentDefinition = JsonConvert.DeserializeObject<EdgeDeploymentDefinition>(crdBody);
                            }
                        }
                    }

                    return false;
                }))
            {
                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var cmd = new EdgeDeploymentCommand(Namespace, ResourceName, client, new[] { dockerModule }, Option.None<EdgeDeploymentDefinition>(), Runtime, configProvider.Object, EdgeletModuleOwner);

                await cmd.ExecuteAsync(CancellationToken.None);

                Assert.True(postCrdCalled);
                Assert.Equal("module1", postedEdgeDeploymentDefinition.Spec[0].Name);
                Assert.Equal("test-image:1", postedEdgeDeploymentDefinition.Spec[0].Config.Image);
                Assert.True(postedEdgeDeploymentDefinition.Spec[0].Env.Contains(new KeyValuePair<string, EnvVal>("ACamelCaseEnvVar", new EnvVal("ACamelCaseEnvVarValue"))));
            }
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteWithAuthReplaceObjects()
        {
            string secretName = "username-docker.io";
            var secretData = new Dictionary<string, byte[]> { [Constants.K8sPullSecretData] = Encoding.UTF8.GetBytes("Invalid Secret Data") };
            var secretMeta = new V1ObjectMeta(name: secretName, namespaceProperty: Namespace);
            IModule dockerModule = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));

            var existingSecret = new V1Secret("v1", secretData, type: Constants.K8sPullSecretType, kind: "Secret", metadata: secretMeta);
            var existingDeployment = new EdgeDeploymentDefinition(Constants.EdgeDeployment.ApiVersion, Constants.EdgeDeployment.Kind, new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>());
            bool getSecretCalled = false;
            bool putSecretCalled = false;
            bool putCrdCalled = false;

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: async httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pathStr.Contains($"api/v1/namespaces/{Namespace}/secrets/{secretName}"))
                        {
                            getSecretCalled = true;
                            await httpContext.Response.Body.WriteAsync(JsonConvert.SerializeObject(existingSecret).ToBody());
                        }
                    }
                    else if (string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.Body = httpContext.Request.Body;
                        if (pathStr.Contains($"api/v1/namespaces/{Namespace}/secrets/{secretName}"))
                        {
                            putSecretCalled = true;
                        }
                        else if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}/{ResourceName}"))
                        {
                            putCrdCalled = true;
                        }
                    }

                    return false;
                }))
            {
                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var cmd = new EdgeDeploymentCommand(Namespace, ResourceName, client, new[] { dockerModule }, Option.Maybe(existingDeployment), Runtime, configProvider.Object, EdgeletModuleOwner);

                await cmd.ExecuteAsync(CancellationToken.None);

                Assert.True(getSecretCalled, nameof(getSecretCalled));
                Assert.True(putSecretCalled, nameof(putSecretCalled));
                Assert.True(putCrdCalled, nameof(putCrdCalled));
            }
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteTwoModulesWithSamePullSecret()
        {
            string secretName = "username-docker.io";
            IModule dockerModule1 = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            IModule dockerModule2 = new DockerModule("module2", "v1", ModuleStatus.Running, RestartPolicy.Always, Config2, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(It.IsAny<DockerModule>(), Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(It.IsAny<DockerModule>(), Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));
            bool getSecretCalled = false;
            bool putSecretCalled = false;
            int postSecretCalled = 0;
            bool putCrdCalled = false;
            int postCrdCalled = 0;
            Stream secretBody = Stream.Null;

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pathStr.Contains($"api/v1/namespaces/{Namespace}/secrets/{secretName}"))
                        {
                            if (secretBody == Stream.Null)
                            {
                                // 1st pass, secret should not exist
                                getSecretCalled = true;
                                httpContext.Response.StatusCode = 404;
                            }
                            else
                            {
                                // 2nd pass, use secret from creation.
                                httpContext.Response.Body = secretBody;
                            }
                        }
                    }
                    else if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.StatusCode = 201;
                        httpContext.Response.Body = httpContext.Request.Body;
                        if (pathStr.Contains($"api/v1/namespaces/{Namespace}/secrets"))
                        {
                            postSecretCalled++;
                            secretBody = httpContext.Request.Body; // save this for next query.
                        }
                        else if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}"))
                        {
                            postCrdCalled++;
                        }
                    }
                    else if (string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.Body = httpContext.Request.Body;
                        if (pathStr.Contains($"api/v1/namespaces/{Namespace}/secrets"))
                        {
                            putSecretCalled = true;
                        }
                        else if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}"))
                        {
                            putCrdCalled = true;
                        }
                    }

                    return Task.FromResult(false);
                }))
            {
                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var cmd = new EdgeDeploymentCommand(Namespace, ResourceName, client, new[] { dockerModule1, dockerModule2 }, Option.None<EdgeDeploymentDefinition>(), Runtime, configProvider.Object, EdgeletModuleOwner);

                await cmd.ExecuteAsync(CancellationToken.None);

                Assert.True(getSecretCalled, nameof(getSecretCalled));
                Assert.Equal(1, postSecretCalled);
                Assert.False(putSecretCalled, nameof(putSecretCalled));
                Assert.Equal(1, postCrdCalled);
                Assert.False(putCrdCalled, nameof(putCrdCalled));
            }
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteEdgeAgentGetsCurrentImage()
        {
            IModule dockerModule = new DockerModule("edgeAgent", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            IRuntimeModule currentModule = new EdgeAgentDockerRuntimeModule(AgentConfig1, ModuleStatus.Running, 0, "description", DateTime.Today, DateTime.Today, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));
            configProvider.Setup(cp => cp.GetCombinedConfig(currentModule, Runtime))
                .Returns(() => new CombinedKubernetesConfig(AgentConfig1.Image, CreatePodParameters.Create(image: AgentConfig1.Image), Option.Maybe(ImagePullSecret)));
            var edgeDefinition = Option.None<EdgeDeploymentDefinition>();
            KubernetesConfig kc = new KubernetesConfig(AgentConfig1.Image, CreatePodParameters.Create(), Option.None<AuthConfig>());
            var edgeDefinitionCurrentModule = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>() { new KubernetesModule(currentModule, kc, EdgeletModuleOwner) }, null);

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.StatusCode = 404;
                    }
                    else if (string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.Body = httpContext.Request.Body;
                        if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}"))
                        {
                            StreamReader reader = new StreamReader(httpContext.Request.Body);
                            string bodyText = reader.ReadToEnd();
                            var body = JsonConvert.DeserializeObject<EdgeDeploymentDefinition>(bodyText);
                            edgeDefinition = Option.Maybe(body);
                        }
                    }

                    return Task.FromResult(false);
                }))
            {
                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var cmd = new EdgeDeploymentCommand(Namespace, ResourceName, client, new[] { dockerModule }, Option.Maybe(edgeDefinitionCurrentModule), Runtime, configProvider.Object, EdgeletModuleOwner);

                await cmd.ExecuteAsync(CancellationToken.None);

                Assert.True(edgeDefinition.HasValue);
                var receivedEdgeDefinition = edgeDefinition.OrDefault();
                var agentModule = receivedEdgeDefinition.Spec[0];
                Assert.Equal(AgentConfig1.Image, agentModule.Config.Image);
            }
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteEdgeAgentDeploymentImageFallback()
        {
            IModule dockerModule = new DockerModule("edgeAgent", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var dockerConfigProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            dockerConfigProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(DockerAuth)));
            var configProvider = new Mock<ICombinedConfigProvider<CombinedKubernetesConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(dockerModule, Runtime))
                .Returns(() => new CombinedKubernetesConfig("test-image:1", CreatePodParameters.Create(image: "test-image:1"), Option.Maybe(ImagePullSecret)));
            Option<EdgeDeploymentDefinition> edgeDefinition = Option.None<EdgeDeploymentDefinition>();
            string agentDeploymentImage = "image:3";
            bool postSecretCalled = false;
            bool postCrdCalled = false;

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: async httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pathStr.Contains($"namespaces/{Namespace}/deployment"))
                        {
                            httpContext.Response.StatusCode = 200;
                            V1Deployment d = new V1Deployment
                            {
                                ApiVersion = "apps/v1",
                                Kind = "Deployment",
                                Metadata = new V1ObjectMeta
                                {
                                    Name = "edgeagent",
                                    NamespaceProperty = Namespace
                                },
                                Spec = new V1DeploymentSpec
                                {
                                    Template = new V1PodTemplateSpec
                                    {
                                        Metadata = new V1ObjectMeta
                                        {
                                            Name = "edgeagent",
                                        },
                                        Spec = new V1PodSpec
                                        {
                                            Containers = new List<V1Container>
                                            {
                                                new V1Container
                                                {
                                                    Image = agentDeploymentImage,
                                                    Name = "edgeagent"
                                                }
                                            }
                                        }
                                    }
                                }
                            };
                            await httpContext.Response.Body.WriteAsync(JsonConvert.SerializeObject(d).ToBody());
                        }
                        else
                        {
                            httpContext.Response.StatusCode = 404;
                        }
                    }
                    else if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.StatusCode = 201;
                        httpContext.Response.Body = httpContext.Request.Body;
                        if (pathStr.Contains($"api/v1/namespaces/{Namespace}/secrets"))
                        {
                            postSecretCalled = true;
                        }
                        else if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}"))
                        {
                            postCrdCalled = true;
                            StreamReader reader = new StreamReader(httpContext.Request.Body);
                            string bodyText = reader.ReadToEnd();
                            var body = JsonConvert.DeserializeObject<EdgeDeploymentDefinition>(bodyText);
                            edgeDefinition = Option.Maybe(body);
                        }
                    }

                    return false;
                }))
            {
                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var cmd = new EdgeDeploymentCommand(Namespace, ResourceName, client, new[] { dockerModule }, Option.None<EdgeDeploymentDefinition>(), Runtime, configProvider.Object, EdgeletModuleOwner);

                await cmd.ExecuteAsync(CancellationToken.None);

                Assert.True(postSecretCalled, nameof(postSecretCalled));
                Assert.True(postCrdCalled, nameof(postCrdCalled));
                Assert.True(edgeDefinition.HasValue);
                var receivedEdgeDefinition = edgeDefinition.OrDefault();
                var agentModule = receivedEdgeDefinition.Spec[0];
                Assert.Equal(agentDeploymentImage, agentModule.Config.Image);
            }
        }
    }
}
