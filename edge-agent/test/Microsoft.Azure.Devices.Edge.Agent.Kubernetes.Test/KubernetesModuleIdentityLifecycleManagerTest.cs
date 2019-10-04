// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class KubernetesModuleIdentityLifecycleManagerTest
    {
        const string IothubHostName = "test.azure-devices.net";
        const string DeviceId = "edgeDevice1";
        const string GatewayHostName = "edgedevicehost";
        static readonly Uri EdgeletUri = new Uri("http://localhost");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly ModuleIdentityProviderServiceBuilder ModuleIdentityProviderServiceBuilder = new ModuleIdentityProviderServiceBuilder(IothubHostName, DeviceId, GatewayHostName);

        [Fact]
        public async Task TestGetModulesIdentityIIdentityManagerExceptionShouldReturnEmptyIdentities()
        {
            // Arrange
            var identityManager = Mock.Of<IIdentityManager>();
            Mock.Get(identityManager).Setup(m => m.GetIdentities()).ThrowsAsync(new InvalidOperationException());
            var moduleIdentityLifecycleManager = new KubernetesModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri);
            var envVar = new Dictionary<string, EnvVal>();

            var module1 = new TestModule("mod1", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            var module2 = new TestModule("mod2", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            var module3 = new TestModule("mod3", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            var module4 = new TestModule("$edgeHub", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            ModuleSet desired = ModuleSet.Create(module1, module4);
            ModuleSet current = ModuleSet.Create(module2, module3, module4);

            // Act
            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desired, current);

            // Assert
            Assert.False(modulesIdentities.Any());
            Mock.Get(identityManager).Verify();
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentityShouldReturnAll()
        {
            // Arrange
            const string Module1 = "module1";
            var identity1 = new Identity(Module1, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            const string Module2 = "module2";
            var identity2 = new Identity(Module2, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            const string Module3 = "module3";
            var identity3 = new Identity(Module3, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            var identityManager = Mock.Of<IIdentityManager>(
                m =>
                    m.GetIdentities() == Task.FromResult(new List<Identity> { identity1, identity2, identity3 }.AsEnumerable()));

            var moduleIdentityLifecycleManager = new KubernetesModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri);
            var envVar = new Dictionary<string, EnvVal>();
            var desiredModule1 = new TestModule(Module1, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            var desiredModule2 = new TestModule(Module2, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            var desiredModule3 = new TestModule(Module3, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            ModuleSet desired = ModuleSet.Create(desiredModule1, desiredModule2, desiredModule3);
            ModuleSet current = ModuleSet.Create(desiredModule1, desiredModule2, desiredModule3);

            // Act
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desired, current);

            // Assert
            Assert.NotNull(moduleIdentities);
            Assert.True(moduleIdentities.TryGetValue(Module1, out IModuleIdentity module1Identity));
            Assert.True(moduleIdentities.TryGetValue(Module2, out IModuleIdentity module2Identity));
            Assert.True(moduleIdentities.TryGetValue(Module3, out IModuleIdentity module3Identity));
            Assert.Equal(Module1, module1Identity.ModuleId);
            Assert.Equal(Module2, module2Identity.ModuleId);
            Assert.Equal(Module3, module3Identity.ModuleId);
            foreach (var moduleIdentity in moduleIdentities)
            {
                Assert.IsType<IdentityProviderServiceCredentials>(moduleIdentity.Value.Credentials);
                Assert.Equal(EdgeletUri.ToString(), ((IdentityProviderServiceCredentials)moduleIdentity.Value.Credentials).ProviderUri);
                Assert.Equal(Option.None<string>(), ((IdentityProviderServiceCredentials)moduleIdentity.Value.Credentials).Version);
            }

            Mock.Get(identityManager).VerifyAll();
        }
    }
}
