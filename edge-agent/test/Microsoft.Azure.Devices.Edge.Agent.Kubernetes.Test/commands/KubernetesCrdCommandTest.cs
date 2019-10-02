// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
    using Moq;
    using Newtonsoft.Json;
    using Xunit;
    using Constants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class KubernetesCrdCommandTest
    {
        const string Namespace = "namespace";
        static readonly ResourceName ResourceName = new ResourceName("hostname", "deviceId");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");
        static readonly DockerConfig Config2 = new DockerConfig("test-image:2");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IKubernetes DefaultClient = Mock.Of<IKubernetes>();
        static readonly ICombinedConfigProvider<CombinedDockerConfig> DefaultConfigProvider = Mock.Of<ICombinedConfigProvider<CombinedDockerConfig>>();
        static readonly IRuntimeInfo Runtime = Mock.Of<IRuntimeInfo>();

        [Fact]
        [Unit]
        public void ConstructorThrowsOnInvalidParams()
        {
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            KubernetesModule km1 = new KubernetesModule(m1 as IModule<DockerConfig>, config);
            KubernetesModule[] modules = { km1 };
            Assert.Throws<ArgumentException>(() => new EdgeDeploymentCommand(null, ResourceName, DefaultClient, modules, Runtime, DefaultConfigProvider));
            Assert.Throws<ArgumentException>(() => new EdgeDeploymentCommand(null, null, DefaultClient, modules, Runtime, DefaultConfigProvider));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(Namespace, ResourceName, null, modules, Runtime, DefaultConfigProvider));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(Namespace, ResourceName, DefaultClient, null, Runtime, DefaultConfigProvider));
            Assert.Throws<ArgumentNullException>(() => new EdgeDeploymentCommand(Namespace, ResourceName, DefaultClient, modules, Runtime, null));
            Assert.Throws<ArgumentException>(() => new EdgeDeploymentCommand(null, ResourceName, DefaultClient, modules, Runtime, DefaultConfigProvider));
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteWithAuthCreateNewObjects()
        {
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule((IModule<DockerConfig>)m1, config);
            KubernetesModule[] modules = { km1 };
            var token = default(CancellationToken);
            var auth = new AuthConfig() { Username = "username", Password = "password", ServerAddress = "docker.io" };
            var configProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(km1, Runtime)).Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(auth)));
            bool getSecretCalled = false;
            bool postSecretCalled = false;
            bool getCrdCalled = false;
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
                        else if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}"))
                        {
                            getCrdCalled = true;
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
                var client = new Kubernetes(
                    new KubernetesClientConfiguration
                    {
                        Host = server.Uri
                    });
                var cmd = new EdgeDeploymentCommand(Namespace, ResourceName, client, modules, Runtime, configProvider.Object);
                await cmd.ExecuteAsync(token);
                Assert.True(getSecretCalled, nameof(getSecretCalled));
                Assert.True(postSecretCalled, nameof(postSecretCalled));
                Assert.True(getCrdCalled, nameof(getCrdCalled));
                Assert.True(postCrdCalled, nameof(postCrdCalled));
            }
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteDeploysModulesWithEnvVars()
        {
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            IDictionary<string, EnvVal> moduleEnvVars = new Dictionary<string, EnvVal>()
            {
                { "ACamelCaseEnvVar", new EnvVal("ACamelCaseEnvVarValue") }
            };

            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, moduleEnvVars);
            var km1 = new KubernetesModule((IModule<DockerConfig>)m1, config);
            KubernetesModule[] modules = { km1 };
            var token = default(CancellationToken);
            var auth = new AuthConfig() { Username = "username", Password = "password", ServerAddress = "docker.io" };
            var configProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(km1, Runtime)).Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(auth)));
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
                var client = new Kubernetes(
                    new KubernetesClientConfiguration
                    {
                        Host = server.Uri
                    });
                var cmd = new EdgeDeploymentCommand(Namespace, ResourceName, client, modules, Runtime, configProvider.Object);
                await cmd.ExecuteAsync(token);

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
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            string secretName = "username-docker.io";
            var secretData = new Dictionary<string, byte[]> { [Constants.K8sPullSecretData] = Encoding.UTF8.GetBytes("Invalid Secret Data") };
            var secretMeta = new V1ObjectMeta(name: secretName, namespaceProperty: Namespace);
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule((IModule<DockerConfig>)m1, config);
            KubernetesModule[] modules = { km1 };
            var token = default(CancellationToken);
            var auth = new AuthConfig() { Username = "username", Password = "password", ServerAddress = "docker.io" };
            var configProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(km1, Runtime)).Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(auth)));
            var existingSecret = new V1Secret("v1", secretData, type: Constants.K8sPullSecretType, kind: "Secret", metadata: secretMeta);
            var existingDeployment = new EdgeDeploymentDefinition(Constants.EdgeDeployment.ApiVersion, Constants.EdgeDeployment.Kind, new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>());
            bool getSecretCalled = false;
            bool putSecretCalled = false;
            bool getCrdCalled = false;
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
                            await httpContext.Response.Body.WriteAsync(JsonConvert.SerializeObject(existingSecret).ToBody(), token);
                        }
                        else if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}/{ResourceName}"))
                        {
                            getCrdCalled = true;
                            await httpContext.Response.Body.WriteAsync(JsonConvert.SerializeObject(existingDeployment).ToBody(), token);
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
                var client = new Kubernetes(
                    new KubernetesClientConfiguration
                    {
                        Host = server.Uri
                    });
                var cmd = new EdgeDeploymentCommand(Namespace, ResourceName, client, modules, Runtime, configProvider.Object);
                await cmd.ExecuteAsync(token);
                Assert.True(getSecretCalled, nameof(getSecretCalled));
                Assert.True(putSecretCalled, nameof(putSecretCalled));
                Assert.True(getCrdCalled, nameof(getCrdCalled));
                Assert.True(putCrdCalled, nameof(putCrdCalled));
            }
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteTwoModulesWithSamePullSecret()
        {
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            string secretName = "username-docker.io";
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule((IModule<DockerConfig>)m1, config);
            IModule m2 = new DockerModule("module2", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config2, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km2 = new KubernetesModule((IModule<DockerConfig>)m2, config);
            KubernetesModule[] modules = { km1, km2 };
            var token = default(CancellationToken);
            var auth = new AuthConfig() { Username = "username", Password = "password", ServerAddress = "docker.io" };
            var configProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(It.IsAny<KubernetesModule>(), Runtime)).Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(auth)));
            bool getSecretCalled = false;
            bool putSecretCalled = false;
            int postSecretCalled = 0;
            bool getCrdCalled = false;
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
                        else if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}/{ResourceName}"))
                        {
                            getCrdCalled = true;
                            httpContext.Response.StatusCode = 404;
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
                var client = new Kubernetes(
                    new KubernetesClientConfiguration
                    {
                        Host = server.Uri.ToString()
                    });
                var cmd = new EdgeDeploymentCommand(Namespace, ResourceName, client, modules, Runtime, configProvider.Object);
                await cmd.ExecuteAsync(token);
                Assert.True(getSecretCalled, nameof(getSecretCalled));
                Assert.Equal(1, postSecretCalled);
                Assert.False(putSecretCalled, nameof(putSecretCalled));
                Assert.True(getCrdCalled, nameof(getCrdCalled));
                Assert.Equal(1, postCrdCalled);
                Assert.False(putCrdCalled, nameof(putCrdCalled));
            }
        }
    }
}
