// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.Watchers
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Moq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using k8s;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using System.Collections.Immutable;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Newtonsoft.Json.Serialization;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    // using Docker.DotNet.Models;
    using Microsoft.Extensions.Configuration;

    [Unit]
    public class CrdWatcherTests
    {
        private const string TestHubHostName = "test-hub";
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");
        static readonly DockerConfig Config2 = new DockerConfig("test-image:2");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");

/*
        [Fact]
        public async Task FirstTest()
        {
            var validIdentities = this.GetValidTestIdentities();
            var newIdentity = validIdentities[0];
            // Remove the new identity from the list to act like we are addding one
            validIdentities.Remove(newIdentity);

            var watcher = GetTestWatcher(validIdentities);

            await watcher.WatchDeploymentEventsAsync(WatchEventType.Added, this.GetSerializedDeploymentDefinition());

            Assert.True(await Task.FromResult(true));
        }

        public CrdWatcher<CombinedDockerModule> GetTestWatcher(List<IModuleIdentity> currentIdentities)
        {
            return new CrdWatcher<CombinedDockerModule>(
                TestHubHostName,
                "test-device",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "testnamespace",
                string.Empty,
                string.Empty,
                new Uri("localhost:1234"),
                new Uri("localhost:1234"),
                this.GetSerDe(),
                BuildModuleIdentityLifecycleManager(currentIdentities),
                this.GetClient());
        }
*/

        private string GetSerializedDeploymentDefinition()
        {
            k8s.Models.V1ObjectMeta metadata = new k8s.Models.V1ObjectMeta();
            List<KubernetesModule<CombinedDockerModule>> kubernetesModules = new List<KubernetesModule<CombinedDockerModule>>();
            EdgeDeploymentDefinition<CombinedDockerModule> definition = new EdgeDeploymentDefinition<CombinedDockerModule>("1.0", "fake", metadata, kubernetesModules);

            return this.GetSerDe().Serialize(definition);
        }

        private IKubernetes GetClient()
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);

            return client.Object;
        }

        private IModuleIdentityLifecycleManager BuildModuleIdentityLifecycleManager(List<IModuleIdentity> currentIdentities)
        {
            IImmutableDictionary<string, IModuleIdentity> identityDictionary = currentIdentities.ToImmutableDictionary(m => m.ModuleId);
            var manager = new Mock<IModuleIdentityLifecycleManager>();
            manager.Setup(m => m.GetModuleIdentitiesAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>())).Returns(Task.FromResult(identityDictionary));

            return manager.Object;
        }

        private List<IModuleIdentity> GetValidTestIdentities()
        {
            string connectionString = "fake";
            ModuleIdentity identity1 = new ModuleIdentity(TestHubHostName, string.Empty, "device1", "module1", new ConnectionStringCredentials(connectionString));
            ModuleIdentity identity2 = new ModuleIdentity(TestHubHostName, string.Empty, "device1", "module2", new ConnectionStringCredentials(connectionString));
            ModuleIdentity identity3 = new ModuleIdentity(TestHubHostName, string.Empty, "device1", "module3", new ConnectionStringCredentials(connectionString));
            ModuleIdentity identity4 = new ModuleIdentity(TestHubHostName, string.Empty, "device1", "module4", new ConnectionStringCredentials(connectionString));

            return new List<IModuleIdentity>() { identity1, identity2, identity3, identity4 };

        }

        private TypeSpecificSerDe<EdgeDeploymentDefinition<CombinedDockerModule>> GetSerDe()
        {
            var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
            {
                [typeof(IModule)] = new Dictionary<string, Type>
                {
                    ["docker"] = typeof(CombinedDockerModule)
                }
            };

            return new TypeSpecificSerDe<EdgeDeploymentDefinition<CombinedDockerModule>>(deserializerTypesMap, new CamelCasePropertyNamesContractResolver());
        }
    }
}
