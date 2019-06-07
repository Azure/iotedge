// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ModuleIdentityLifecycleManagerTest
    {
        const string IothubHostName = "test.azure-devices.net";
        const string DeviceId = "edgeDevice1";
        const string GatewayHostName = "edgedevicehost";
        static readonly Uri EdgeletUri = new Uri("http://localhost");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly ModuleIdentityProviderServiceBuilder ModuleIdentityProviderServiceBuilder = new ModuleIdentityProviderServiceBuilder(IothubHostName, DeviceId, GatewayHostName);

        [Fact]
        public async Task TestGetModulesIdentity_WithEmptyDiff_ShouldReturnEmptyIdentities()
        {
            // Arrange
            var identityManager = Mock.Of<IIdentityManager>(m => m.GetIdentities() == Task.FromResult(Enumerable.Empty<Identity>()));
            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri);

            // Act
            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(ModuleSet.Empty, ModuleSet.Empty);

            // Assert
            Assert.True(!modulesIdentities.Any());
            Mock.Get(identityManager).Verify();
        }

        [Fact]
        public async Task TestGetModulesIdentity_IIdentityManagerException_ShouldReturnEmptyIdentities()
        {
            // Arrange
            var identityManager = Mock.Of<IIdentityManager>();
            Mock.Get(identityManager).Setup(m => m.GetIdentities()).ThrowsAsync(new InvalidOperationException());
            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri);
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
        public async Task TestGetModulesIdentity_WithNewModules_ShouldCreateIdentities()
        {
            // Arrange
            const string Name = "module1";
            var identity = new Identity(
                Name,
                Guid.NewGuid().ToString(),
                Constants.ModuleIdentityEdgeManagedByValue);

            var identityManager = Mock.Of<IIdentityManager>(
                m =>
                    m.GetIdentities() == Task.FromResult(Enumerable.Empty<Identity>()) &&
                    m.CreateIdentityAsync(Name, Constants.ModuleIdentityEdgeManagedByValue) == Task.FromResult(identity));

            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri);
            var module = new TestModule(Name, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, new Dictionary<string, EnvVal>());

            // Act
            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(
                ModuleSet.Create(new IModule[] { module }),
                ModuleSet.Empty);

            // Assert
            Assert.True(modulesIdentities.Count() == 1);
            Assert.True(modulesIdentities.TryGetValue(Name, out IModuleIdentity moduleIdentity));
            Assert.Equal(moduleIdentity.ModuleId, Name);
            Assert.IsType<IdentityProviderServiceCredentials>(moduleIdentity.Credentials);
            Assert.Equal(EdgeletUri.ToString(), ((IdentityProviderServiceCredentials)moduleIdentity.Credentials).ProviderUri);
            Assert.Equal(Option.None<string>(), ((IdentityProviderServiceCredentials)moduleIdentity.Credentials).Version);
            Mock.Get(identityManager).Verify();
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithUpdatedModules_ShouldUpdateIdentities()
        {
            // Arrange
            const string Module1 = "module1";
            var identity1 = new Identity(Module1, Guid.NewGuid().ToString(), "IotEdge");

            const string Module2 = "module2";
            var identity2 = new Identity(Module2, Guid.NewGuid().ToString(), "Me");

            const string Module3 = "module3";
            var identity3 = new Identity(Module3, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            const string Module4 = "$edgeHub";
            var identity4 = new Identity(Module4, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            // We should NOT get an update request for this identity
            const string Module5 = "$edgeAgent";
            var identity5 = new Identity(Module5, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            var identityManager = Mock.Of<IIdentityManager>(
                m =>
                    m.GetIdentities() == Task.FromResult(new List<Identity>() { identity2, identity3, identity4, identity5 }.AsEnumerable()) &&
                    m.CreateIdentityAsync(Module1, Constants.ModuleIdentityEdgeManagedByValue) == Task.FromResult(identity1) &&
                    m.UpdateIdentityAsync(identity2.ModuleId, identity2.GenerationId, identity2.ManagedBy) == Task.FromResult(identity2) &&
                    m.UpdateIdentityAsync(identity3.ModuleId, identity3.GenerationId, identity3.ManagedBy) == Task.FromResult(identity3) &&
                    m.UpdateIdentityAsync(identity4.ModuleId, identity4.GenerationId, identity4.ManagedBy) == Task.FromResult(identity4));

            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri);
            var envVar = new Dictionary<string, EnvVal>();
            var module1 = new TestModule(Module1, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            var module2 = new TestModule(Module2, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            var module3 = new TestModule(Module3, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            var module4 = new TestModule(Module4, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            var module5 = new TestModule(Module5, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            ModuleSet desired = ModuleSet.Create(module1, module2.CloneWithImage("image2"), module3.CloneWithImage("image2"), module4.CloneWithImage("image2"), module5.CloneWithImage("image2"));
            ModuleSet current = ModuleSet.Create(module2, module3, module4, module5);

            // Act
            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desired, current);

            // Assert
            Assert.Equal(5, modulesIdentities.Count);
            Assert.True(modulesIdentities.TryGetValue(Module1, out IModuleIdentity moduleIdentity1));
            Assert.Equal(Module1, moduleIdentity1.ModuleId);
            Assert.True(modulesIdentities.TryGetValue(Module2, out IModuleIdentity moduleIdentity2));
            Assert.Equal(Module2, moduleIdentity2.ModuleId);
            Assert.True(modulesIdentities.TryGetValue(Module3, out IModuleIdentity moduleIdentity3));
            Assert.Equal(Module3, moduleIdentity3.ModuleId);
            Assert.True(modulesIdentities.TryGetValue("edgeHub", out IModuleIdentity moduleIdentity4));
            Assert.Equal(Module4, moduleIdentity4.ModuleId);
            Assert.IsType<IdentityProviderServiceCredentials>(moduleIdentity1.Credentials);
            Mock.Get(identityManager).Verify();
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithRemovedModules_ShouldRemove()
        {
            // Arrange
            const string Module1 = "module1";
            var identity1 = new Identity(Module1, Guid.NewGuid().ToString(), "IotEdge");

            const string Module2 = "module2";
            var identity2 = new Identity(Module2, Guid.NewGuid().ToString(), "Me");

            const string Module3 = "module3";
            var identity3 = new Identity(Module3, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            var identityManager = Mock.Of<IIdentityManager>(
                m =>
                    m.GetIdentities() == Task.FromResult(new List<Identity>() { identity2, identity3 }.AsEnumerable()) &&
                    m.CreateIdentityAsync(Module1, It.IsAny<string>()) == Task.FromResult(identity1) &&
                    m.DeleteIdentityAsync(Module3) == Task.FromResult(identity3));

            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri);
            var envVar = new Dictionary<string, EnvVal>();
            var desiredModule = new TestModule(Module1, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            var currentModule1 = new TestModule(Module2, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            var currentModule2 = new TestModule(Module3, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, envVar);
            ModuleSet desired = ModuleSet.Create(new IModule[] { desiredModule });
            ModuleSet current = ModuleSet.Create(new IModule[] { currentModule1, currentModule2 });

            // Act
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desired, current);

            // Assert
            Assert.NotNull(moduleIdentities);
            Assert.True(moduleIdentities.TryGetValue(Module1, out IModuleIdentity module1Identity));
            Assert.Equal(Module1, module1Identity.ModuleId);
            Assert.IsType<IdentityProviderServiceCredentials>(module1Identity.Credentials);
            Assert.Equal(EdgeletUri.ToString(), ((IdentityProviderServiceCredentials)module1Identity.Credentials).ProviderUri);
            Assert.Equal(Option.None<string>(), ((IdentityProviderServiceCredentials)module1Identity.Credentials).Version);

            Mock.Get(identityManager).Verify(im => im.DeleteIdentityAsync(Module3));
        }
    }
}
