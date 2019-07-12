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
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Commands;
    using Xunit;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Constants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class KubernetesCrdCommandTest
    {
        const string Ns = "namespace";
        const string Hostname = "hostname";
        const string GwHostname = "gwHostname";
        const string DeviceId = "deviceId";
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");
        static readonly DockerConfig Config2 = new DockerConfig("test-image:2");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IRuntimeInfo RuntimeInfo = Mock.Of<IRuntimeInfo>();
        static readonly IKubernetes DefaultClient = Mock.Of<IKubernetes>();
        static readonly ICommandFactory DefaultCommandFactory = new KubernetesCommandFactory();
        static readonly ICombinedConfigProvider<CombinedDockerConfig> DefaultConfigProvider = Mock.Of<ICombinedConfigProvider<CombinedDockerConfig>>();
        static readonly IRuntimeInfo Runtime = Mock.Of<IRuntimeInfo>();

        [Fact]
        [Unit]
        public void CrdCommandCreateValidation()
        {
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            KubernetesModule<DockerConfig> km1 = new KubernetesModule<DockerConfig>(m1 as IModule<DockerConfig>);
            KubernetesModule<DockerConfig>[] modules = { km1 };
            Assert.Throws<ArgumentException>(() => new KubernetesCrdCommand<CombinedDockerConfig>(null, Hostname, DeviceId, DefaultClient, modules, Option.None<IRuntimeInfo>(), DefaultConfigProvider));
            Assert.Throws<ArgumentException>(() => new KubernetesCrdCommand<CombinedDockerConfig>(Ns, null, DeviceId, DefaultClient, modules, Option.None<IRuntimeInfo>(), DefaultConfigProvider));
            Assert.Throws<ArgumentException>(() => new KubernetesCrdCommand<CombinedDockerConfig>(Ns, Hostname, null, DefaultClient, modules, Option.None<IRuntimeInfo>(), DefaultConfigProvider));
            Assert.Throws<ArgumentNullException>(() => new KubernetesCrdCommand<CombinedDockerConfig>(Ns, Hostname, DeviceId, null, modules, Option.None<IRuntimeInfo>(), DefaultConfigProvider));
            Assert.Throws<ArgumentNullException>(() => new KubernetesCrdCommand<CombinedDockerConfig>(Ns, Hostname, DeviceId, DefaultClient, null, Option.None<IRuntimeInfo>(), DefaultConfigProvider));
            Assert.Throws<ArgumentNullException>(() => new KubernetesCrdCommand<CombinedDockerConfig>(Ns, Hostname, DeviceId, DefaultClient, modules, Option.None<IRuntimeInfo>(), null));
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteAsyncInvalidModule()
        {
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule<DockerConfig>(m1 as IModule<DockerConfig>);
            KubernetesModule<DockerConfig>[] modules = { km1 };
            Option<IRuntimeInfo> runtimeOption = Option.Maybe(Runtime);
            var configProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(km1, Runtime)).Returns(() => null);

            var token = new CancellationToken();
            var cmd = new KubernetesCrdCommand<CombinedDockerConfig>(Ns, Hostname, DeviceId, DefaultClient, modules, runtimeOption, DefaultConfigProvider);
            await Assert.ThrowsAsync<InvalidModuleException>(() => cmd.ExecuteAsync(token));
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteWithAuthCreateNewObjects()
        {
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule<DockerConfig>((IModule<DockerConfig>)m1);
            KubernetesModule<DockerConfig>[] modules = { km1 };
            var token = new CancellationToken();
            Option<IRuntimeInfo> runtimeOption = Option.Maybe(Runtime);
            var auth = new AuthConfig() { Username = "username", Password = "password", ServerAddress = "docker.io" };
            var configProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(km1, Runtime)).Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(auth)));
            bool getSecretCalled = false;
            bool postSecretCalled = false;
            bool getCrdCalled = false;
            bool postCrdCalled = false;

            using (var server = new MockKubeApiServer(
                resp: string.Empty,
                shouldNext: httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.StatusCode = 404;
                        if (pathStr.Contains($"api/v1/namespaces/{Ns}/secrets"))
                        {
                            getSecretCalled = true;
                        }
                        else if (pathStr.Contains($"namespaces/{Ns}/{Constants.K8sCrdPlural}"))
                        {
                            getCrdCalled = true;
                        }
                    }
                    else if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.StatusCode = 201;
                        httpContext.Response.Body = httpContext.Request.Body;
                        if (pathStr.Contains($"api/v1/namespaces/{Ns}/secrets"))
                        {
                            postSecretCalled = true;
                        }
                        else if (pathStr.Contains($"namespaces/{Ns}/{Constants.K8sCrdPlural}"))
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
                        Host = server.Uri.ToString()
                    });
                var cmd = new KubernetesCrdCommand<CombinedDockerConfig>(Ns, Hostname, DeviceId, client, modules, runtimeOption, configProvider.Object);
                await cmd.ExecuteAsync(token);
                Assert.True(getSecretCalled, nameof(getSecretCalled));
                Assert.True(postSecretCalled, nameof(postSecretCalled));
                Assert.True(getCrdCalled, nameof(getCrdCalled));
                Assert.True(postCrdCalled, nameof(postCrdCalled));
            }
        }

        [Fact]
        [Unit]
        public async void CrdCommandExecuteWithAuthReplaceObjects()
        {
            string resourceName = Hostname + Constants.K8sNameDivider + DeviceId.ToLower();
            string metaApiVersion = Constants.K8sApi + "/" + Constants.K8sApiVersion;
            string secretName = "username-docker.io";
            var secretData = new Dictionary<string, byte[]> { [Constants.K8sPullSecretData] = Encoding.UTF8.GetBytes("Invalid Secret Data") };
            var secretMeta = new V1ObjectMeta(name: secretName, namespaceProperty: Ns);
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule<DockerConfig>((IModule<DockerConfig>)m1);
            KubernetesModule<DockerConfig>[] modules = { km1 };
            var token = new CancellationToken();
            Option<IRuntimeInfo> runtimeOption = Option.Maybe(Runtime);
            var auth = new AuthConfig() { Username = "username", Password = "password", ServerAddress = "docker.io" };
            var configProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(km1, Runtime)).Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(auth)));
            var existingSecret = new V1Secret("v1", secretData, type: Constants.K8sPullSecretType, kind: "Secret", metadata: secretMeta);
            var existingDeployment = new EdgeDeploymentDefinition<DockerConfig>(metaApiVersion, Constants.K8sCrdKind, new V1ObjectMeta(name: resourceName), new List<KubernetesModule<DockerConfig>>());
            bool getSecretCalled = false;
            bool putSecretCalled = false;
            bool getCrdCalled = false;
            bool putCrdCalled = false;

            using (var server = new MockKubeApiServer(
                resp: string.Empty,
                shouldNext: async httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pathStr.Contains($"api/v1/namespaces/{Ns}/secrets/{secretName}"))
                        {
                            getSecretCalled = true;
                            await httpContext.Response.Body.WriteAsync(JsonConvert.SerializeObject(existingSecret).ToBody(), token);
                        }
                        else if (pathStr.Contains($"namespaces/{Ns}/{Constants.K8sCrdPlural}/{resourceName}"))
                        {
                            getCrdCalled = true;
                            await httpContext.Response.Body.WriteAsync(JsonConvert.SerializeObject(existingDeployment).ToBody(), token);
                        }
                    }
                    else if (string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.Body = httpContext.Request.Body;
                        if (pathStr.Contains($"api/v1/namespaces/{Ns}/secrets/{secretName}"))
                        {
                            putSecretCalled = true;
                        }
                        else if (pathStr.Contains($"namespaces/{Ns}/{Constants.K8sCrdPlural}/{resourceName}"))
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
                        Host = server.Uri.ToString()
                    });
                var cmd = new KubernetesCrdCommand<CombinedDockerConfig>(Ns, Hostname, DeviceId, client, modules, runtimeOption, configProvider.Object);
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
            string resourceName = Hostname + Constants.K8sNameDivider + DeviceId.ToLower();
            string secretName = "username-docker.io";
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule<DockerConfig>((IModule<DockerConfig>)m1);
            IModule m2 = new DockerModule("module2", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config2, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km2 = new KubernetesModule<DockerConfig>((IModule<DockerConfig>)m2);
            KubernetesModule<DockerConfig>[] modules = { km1, km2 };
            var token = new CancellationToken();
            Option<IRuntimeInfo> runtimeOption = Option.Maybe(Runtime);
            var auth = new AuthConfig() { Username = "username", Password = "password", ServerAddress = "docker.io" };
            var configProvider = new Mock<ICombinedConfigProvider<CombinedDockerConfig>>();
            configProvider.Setup(cp => cp.GetCombinedConfig(It.IsAny<KubernetesModule<DockerConfig>>(), Runtime)).Returns(() => new CombinedDockerConfig("test-image:1", Config1.CreateOptions, Option.Maybe(auth)));
            bool getSecretCalled = false;
            bool putSecretCalled = false;
            int postSecretCalled = 0;
            bool getCrdCalled = false;
            bool putCrdCalled = false;
            int postCrdCalled = 0;
            Stream secretBody = Stream.Null;

            using (var server = new MockKubeApiServer(
                resp: string.Empty,
                shouldNext: httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pathStr.Contains($"api/v1/namespaces/{Ns}/secrets/{secretName}"))
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
                        else if (pathStr.Contains($"namespaces/{Ns}/{Constants.K8sCrdPlural}/{resourceName}"))
                        {
                            getCrdCalled = true;
                            httpContext.Response.StatusCode = 404;
                        }
                    }
                    else if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.StatusCode = 201;
                        httpContext.Response.Body = httpContext.Request.Body;
                        if (pathStr.Contains($"api/v1/namespaces/{Ns}/secrets"))
                        {
                            postSecretCalled++;
                            secretBody = httpContext.Request.Body; // save this for next query.
                        }
                        else if (pathStr.Contains($"namespaces/{Ns}/{Constants.K8sCrdPlural}"))
                        {
                            postCrdCalled++;
                        }
                    }
                    else if (string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.Body = httpContext.Request.Body;
                        if (pathStr.Contains($"api/v1/namespaces/{Ns}/secrets"))
                        {
                            putSecretCalled = true;
                        }
                        else if (pathStr.Contains($"namespaces/{Ns}/{Constants.K8sCrdPlural}"))
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
                var cmd = new KubernetesCrdCommand<CombinedDockerConfig>(Ns, Hostname, DeviceId, client, modules, runtimeOption, configProvider.Object);
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
