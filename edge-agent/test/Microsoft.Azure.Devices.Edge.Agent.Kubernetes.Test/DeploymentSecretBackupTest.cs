// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Rest;
    using Moq;
    using Xunit;

    using KubeConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class DeploymentSecretBackupTest
    {
        const string TestType = "test";
        const string DefaultSecretName = "default-secret-name";
        const string DefaultNamespace = "default-namespace";
        static readonly KubernetesModuleOwner DefaultOwner = new KubernetesModuleOwner("v1", "Deployment", "Owner", "UID");

        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly ConfigurationInfo ConfigurationInfo = new ConfigurationInfo();
        static readonly IEdgeAgentModule EdgeAgentModule = new TestAgentModule("edgeAgent", "test", new TestConfig("edge-agent"), ImagePullPolicy.OnCreate, ConfigurationInfo, EnvVars);
        static readonly TestRuntimeInfo TestRuntimeInfo = new TestRuntimeInfo("test");
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly IModule ValidModule1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, ConfigurationInfo, EnvVars);
        static readonly IEdgeHubModule EdgeHubModule = new TestHubModule("edgeHub", "test", ModuleStatus.Running, new TestConfig("edge-hub:latest"), RestartPolicy.Always, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, ConfigurationInfo, EnvVars);
        static readonly IDictionary<string, IModule> Modules1 = new Dictionary<string, IModule> { ["mod1"] = ValidModule1 };
        static readonly IDictionary<string, IModule> Modules2 = new Dictionary<string, IModule> { ["mod2"] = ValidModule1 };
        static readonly DeploymentConfig ValidConfig1 = new DeploymentConfig("1.0", TestRuntimeInfo, new SystemModules(EdgeAgentModule, EdgeHubModule), Modules1);
        static readonly DeploymentConfig ValidConfig2 = new DeploymentConfig("1.0", TestRuntimeInfo, new SystemModules(EdgeAgentModule, EdgeHubModule), Modules2);
        static readonly DeploymentConfigInfo ValidConfigInfo1 = new DeploymentConfigInfo(0, ValidConfig1);
        static readonly DeploymentConfigInfo ValidConfigInfo2 = new DeploymentConfigInfo(0, ValidConfig2);

        [Fact]
        public void CreateFailsOnEmptyInput()
        {
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var client = Mock.Of<IKubernetes>();

            Assert.Throws<ArgumentException>(() => new DeploymentSecretBackup(null, DefaultNamespace, DefaultOwner, serde, client));
            Assert.Throws<ArgumentException>(() => new DeploymentSecretBackup("  ", DefaultNamespace, DefaultOwner, serde, client));
            Assert.Throws<ArgumentException>(() => new DeploymentSecretBackup(DefaultSecretName, null, DefaultOwner, serde, client));
            Assert.Throws<ArgumentException>(() => new DeploymentSecretBackup(DefaultSecretName, "  ", DefaultOwner, serde, client));
            Assert.Throws<ArgumentNullException>(() => new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, null, serde, client));
            Assert.Throws<ArgumentNullException>(() => new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, DefaultOwner, null, client));
            Assert.Throws<ArgumentNullException>(() => new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, DefaultOwner, serde, null));
        }

        [Fact]
        public void NameIsSecretName()
        {
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var client = Mock.Of<IKubernetes>();

            var backupSource = new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, DefaultOwner, serde, client);

            Assert.Equal(DefaultSecretName, backupSource.Name);
        }

        [Fact]
        public async void ReadDeploymentConfigFromSecret()
        {
            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            var secretData = new Dictionary<string, byte[]>
            {
                ["backup.json"] = System.Text.Encoding.UTF8.GetBytes(serde.Serialize(ValidConfigInfo1))
            };
            var secret = new V1Secret(data: secretData);
            var response = new HttpOperationResponse<V1Secret>()
            {
                Body = secret,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            client.Setup(c => c.ReadNamespacedSecretWithHttpMessagesAsync(DefaultSecretName, DefaultNamespace, It.IsAny<bool?>(), It.IsAny<bool?>(), null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var backupSource = new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, DefaultOwner, serde, client.Object);
            var deploymentConfigInfo = await backupSource.ReadFromBackupAsync();

            string returnedJson = serde.Serialize(deploymentConfigInfo);
            string expectedJson = serde.Serialize(ValidConfigInfo1);

            Assert.Equal(expectedJson, returnedJson, ignoreCase: true);
            client.VerifyAll();
        }

        [Fact]
        public async void NullSecretreturnsEmptyConfig()
        {
            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            var response = new HttpOperationResponse<V1Secret>()
            {
                Body = null,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            client.Setup(c => c.ReadNamespacedSecretWithHttpMessagesAsync(DefaultSecretName, DefaultNamespace, It.IsAny<bool?>(), It.IsAny<bool?>(), null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var backupSource = new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, DefaultOwner, serde, client.Object);
            var deploymentConfigInfo = await backupSource.ReadFromBackupAsync();

            Assert.NotNull(deploymentConfigInfo);
            Assert.Equal(DeploymentConfigInfo.Empty, deploymentConfigInfo);
            client.VerifyAll();
        }

        [Fact]
        public async void SecretDataNoKeyReturnsEmptyConfig()
        {
            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            var secretData = new Dictionary<string, byte[]>
            {
                ["no match"] = System.Text.Encoding.UTF8.GetBytes(serde.Serialize(ValidConfigInfo1))
            };
            var secret = new V1Secret(data: secretData);
            var response = new HttpOperationResponse<V1Secret>()
            {
                Body = secret,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            client.Setup(c => c.ReadNamespacedSecretWithHttpMessagesAsync(DefaultSecretName, DefaultNamespace, It.IsAny<bool?>(), It.IsAny<bool?>(), null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var backupSource = new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, DefaultOwner, serde, client.Object);
            var deploymentConfigInfo = await backupSource.ReadFromBackupAsync();

            Assert.NotNull(deploymentConfigInfo);
            Assert.Equal(DeploymentConfigInfo.Empty, deploymentConfigInfo);
            client.VerifyAll();
        }

        [Fact]
        public async void DeserializeFaillureReturnsEmptyConfig()
        {
            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            var secretData = new Dictionary<string, byte[]>
            {
                ["backup.json"] = System.Text.Encoding.UTF8.GetBytes("{}")
            };
            var secret = new V1Secret(data: secretData);
            var response = new HttpOperationResponse<V1Secret>()
            {
                Body = secret,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            client.Setup(c => c.ReadNamespacedSecretWithHttpMessagesAsync(DefaultSecretName, DefaultNamespace, It.IsAny<bool?>(), It.IsAny<bool?>(), null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var backupSource = new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, DefaultOwner, serde, client.Object);
            var deploymentConfigInfo = await backupSource.ReadFromBackupAsync();

            Assert.NotNull(deploymentConfigInfo);
            Assert.Equal(DeploymentConfigInfo.Empty, deploymentConfigInfo);
            client.VerifyAll();
        }

        [Fact]
        public async void BackupDeploymentConfigCreatesNewSecret()
        {
            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            string expectedJson = serde.Serialize(ValidConfigInfo1);
            var secretData = new Dictionary<string, byte[]>
            {
                ["backup.json"] = System.Text.Encoding.UTF8.GetBytes(expectedJson)
            };
            var secret = new V1Secret(data: secretData);
            var createResponse = new HttpOperationResponse<V1Secret>()
            {
                Body = secret,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            byte[] receivedData = default(byte[]);
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            client.Setup(c => c.ReadNamespacedSecretWithHttpMessagesAsync(DefaultSecretName, DefaultNamespace, It.IsAny<bool?>(), It.IsAny<bool?>(), null, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpOperationException("Not Found"));
            client.Setup(c => c.CreateNamespacedSecretWithHttpMessagesAsync(It.IsAny<V1Secret>(), DefaultNamespace, null, null, null, null, It.IsAny<CancellationToken>()))
                .Callback((V1Secret body, string namespaceParameter, string dryRun, string fieldManager, string pretty, Dictionary<string, List<string>> customHeaders, CancellationToken cancellationToken) =>
                {
                    Assert.True(body.Data != null);
                    Assert.True(body.Data.TryGetValue("backup.json", out receivedData));
                })
                .ReturnsAsync(createResponse);

            var backupSource = new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, DefaultOwner, serde, client.Object);
            await backupSource.BackupDeploymentConfigAsync(ValidConfigInfo1);

            string backupJson = System.Text.Encoding.UTF8.GetString(receivedData);

            Assert.Equal(expectedJson, backupJson, ignoreCase: true);
            client.VerifyAll();
        }

        [Fact]
        public async void BackupDeploymentConfigReplacesSecret()
        {
            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            string expectedJson = serde.Serialize(ValidConfigInfo1);
            var readSecretData = new Dictionary<string, byte[]>
            {
                ["backup.json"] = System.Text.Encoding.UTF8.GetBytes(serde.Serialize(ValidConfigInfo2))
            };
            var readSecret = new V1Secret(data: readSecretData);
            var replaceSecretData = new Dictionary<string, byte[]>
            {
                ["backup.json"] = System.Text.Encoding.UTF8.GetBytes(expectedJson)
            };
            var replaceSecret = new V1Secret(data: readSecretData);
            var readResponse = new HttpOperationResponse<V1Secret>()
            {
                Body = readSecret,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };
            var replaceResponse = new HttpOperationResponse<V1Secret>()
            {
                Body = replaceSecret,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            byte[] receivedData = default(byte[]);
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            client.Setup(c => c.ReadNamespacedSecretWithHttpMessagesAsync(DefaultSecretName, DefaultNamespace, It.IsAny<bool?>(), It.IsAny<bool?>(), null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(readResponse);
            client.Setup(c => c.ReplaceNamespacedSecretWithHttpMessagesAsync(It.IsAny<V1Secret>(), DefaultSecretName, DefaultNamespace, null, null, null, null, It.IsAny<CancellationToken>()))
                .Callback((V1Secret body, string name, string namespaceParameter, string dryRun, string fieldManager, string pretty, Dictionary<string, List<string>> customHeaders, CancellationToken cancellationToken) =>
                {
                    Assert.True(body.Data != null);
                    Assert.True(body.Data.TryGetValue("backup.json", out receivedData));
                })
                .ReturnsAsync(replaceResponse);

            var backupSource = new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, DefaultOwner, serde, client.Object);
            await backupSource.BackupDeploymentConfigAsync(ValidConfigInfo1);

            string backupJson = System.Text.Encoding.UTF8.GetString(receivedData);

            Assert.Equal(expectedJson, backupJson, ignoreCase: true);
            client.VerifyAll();
        }

        [Fact]
        public async void BackupDeploymentConfigDoesNotReplacesSameSecret()
        {
            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            string expectedJson = serde.Serialize(ValidConfigInfo1);
            var readSecretData = new Dictionary<string, byte[]>
            {
                ["backup.json"] = System.Text.Encoding.UTF8.GetBytes(expectedJson)
            };
            var readSecret = new V1Secret(data: readSecretData);
            var readResponse = new HttpOperationResponse<V1Secret>()
            {
                Body = readSecret,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            client.Setup(c => c.ReadNamespacedSecretWithHttpMessagesAsync(DefaultSecretName, DefaultNamespace, It.IsAny<bool?>(), It.IsAny<bool?>(), null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(readResponse);

            var backupSource = new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, DefaultOwner, serde, client.Object);
            await backupSource.BackupDeploymentConfigAsync(ValidConfigInfo1);

            client.VerifyAll();
        }

        [Fact]
        public async void BackupDeploymentDoesNotThrowOnFailure()
        {
            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            string expectedJson = serde.Serialize(ValidConfigInfo1);
            var readSecretData = new Dictionary<string, byte[]>
            {
                ["backup.json"] = System.Text.Encoding.UTF8.GetBytes(serde.Serialize(ValidConfigInfo2))
            };
            var readSecret = new V1Secret(data: readSecretData);
            var replaceSecretData = new Dictionary<string, byte[]>
            {
                ["backup.json"] = System.Text.Encoding.UTF8.GetBytes(expectedJson)
            };
            var replaceSecret = new V1Secret(data: readSecretData);
            var readResponse = new HttpOperationResponse<V1Secret>()
            {
                Body = readSecret,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            client.Setup(c => c.ReadNamespacedSecretWithHttpMessagesAsync(DefaultSecretName, DefaultNamespace, It.IsAny<bool?>(), It.IsAny<bool?>(), null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(readResponse);
            client.Setup(c => c.ReplaceNamespacedSecretWithHttpMessagesAsync(It.IsAny<V1Secret>(), DefaultSecretName, DefaultNamespace, null, null, null, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpOperationException("Not Permitted"));

            var backupSource = new DeploymentSecretBackup(DefaultSecretName, DefaultNamespace, DefaultOwner, serde, client.Object);
            await backupSource.BackupDeploymentConfigAsync(ValidConfigInfo1);

            client.VerifyAll();
        }

        ISerde<DeploymentConfigInfo> GetSerde()
        {
            var moduleDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestModule)
            };

            var edgeAgentDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestAgentModule)
            };

            var edgeHubDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestHubModule)
            };

            var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestRuntimeInfo)
            };

            var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
            {
                [typeof(IModule)] = moduleDeserializerTypes,
                [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
            };

            ISerde<DeploymentConfigInfo> serde = new TypeSpecificSerDe<DeploymentConfigInfo>(deserializerTypesMap);
            return serde;
        }
    }

    class TestRuntimeInfo : IRuntimeInfo
    {
        public TestRuntimeInfo(string type)
        {
            this.Type = type;
        }

        public string Type { get; }

        public static bool operator ==(TestRuntimeInfo left, TestRuntimeInfo right) => Equals(left, right);

        public static bool operator !=(TestRuntimeInfo left, TestRuntimeInfo right) => !Equals(left, right);

        public bool Equals(IRuntimeInfo other) => other is TestRuntimeInfo otherRuntimeInfo
                                                  && this.Equals(otherRuntimeInfo);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return this.Equals((TestRuntimeInfo)obj);
        }

        public override int GetHashCode()
        {
            return this.Type != null ? this.Type.GetHashCode() : 0;
        }

        public bool Equals(TestRuntimeInfo other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.Type, other.Type);
        }
    }
}
