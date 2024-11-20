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
        static readonly Uri EdgeletUri = new Uri("http://localhost");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly ModuleIdentityProviderServiceBuilder ModuleIdentityProviderServiceBuilder = new ModuleIdentityProviderServiceBuilder(IothubHostName, DeviceId);

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestGetModulesIdentity_WithEmptyDiffAndEmptyCurrent_ShouldReturnEmptyIdentities(bool enableOrphanedIdentityCleanup)
        {
            // Arrange
            var identityManager = Mock.Of<IIdentityManager>(m => m.GetIdentities() == Task.FromResult(Enumerable.Empty<Identity>()));
            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri, enableOrphanedIdentityCleanup);

            // Act
            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(ModuleSet.Empty, ModuleSet.Empty);

            // Assert
            Assert.True(!modulesIdentities.Any());
            Mock.Get(identityManager).Verify();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestGetModulesIdentity_IIdentityManagerException_ShouldReturnEmptyIdentities(bool enableOrphanedIdentityCleanup)
        {
            // Arrange
            var identityManager = Mock.Of<IIdentityManager>();
            Mock.Get(identityManager).Setup(m => m.GetIdentities()).ThrowsAsync(new InvalidOperationException());
            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri, enableOrphanedIdentityCleanup);
            var envVar = new Dictionary<string, EnvVal>();

            var module1 = new TestModule("mod1", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var module2 = new TestModule("mod2", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var module3 = new TestModule("mod3", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var module4 = new TestModule("$edgeHub", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            ModuleSet desired = ModuleSet.Create(module1, module4);
            ModuleSet current = ModuleSet.Create(module2, module3, module4);

            // Act
            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desired, current);

            // Assert
            Assert.False(modulesIdentities.Any());
            Mock.Get(identityManager).Verify();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Unit]
        public async Task TestGetModulesIdentity_WithNewModules_ShouldCreateIdentities(bool enableOrphanedIdentityCleanup)
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

            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri, enableOrphanedIdentityCleanup);
            var module = new TestModule(Name, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, new Dictionary<string, EnvVal>());

            // Act
            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(
                ModuleSet.Create(new IModule[] { module }),
                ModuleSet.Empty);

            // Assert
            Assert.True(modulesIdentities.Count() == 1);
            Assert.True(modulesIdentities.TryGetValue(Name, out IModuleIdentity moduleIdentity));
            Assert.Equal(Name, moduleIdentity.ModuleId);
            Assert.IsType<IdentityProviderServiceCredentials>(moduleIdentity.Credentials);
            Assert.Equal(EdgeletUri.ToString(), ((IdentityProviderServiceCredentials)moduleIdentity.Credentials).ProviderUri);
            Assert.Equal(Option.None<string>(), ((IdentityProviderServiceCredentials)moduleIdentity.Credentials).Version);
            Mock.Get(identityManager).Verify();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Unit]
        public async Task TestGetModulesIdentity_WithUpdatedModules_ShouldUpdateIdentities(bool enableOrphanedIdentityCleanup)
        {
            // Arrange
            const string Module1 = "module1";
            var identity1 = new Identity(Module1, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

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

            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri, enableOrphanedIdentityCleanup);
            var envVar = new Dictionary<string, EnvVal>();
            var module1 = new TestModule(Module1, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var module2 = new TestModule(Module2, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var module3 = new TestModule(Module3, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var module4 = new TestModule(Module4, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var module5 = new TestModule(Module5, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Unit]
        public async Task TestGetModulesIdentity_WithRemovedModules_ShouldRemove(bool enableOrphanedIdentityCleanup)
        {
            // Arrange
            const string Module1 = "module1";
            var identity1 = new Identity(Module1, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            const string Module2 = "module2";
            var identity2 = new Identity(Module2, Guid.NewGuid().ToString(), "Me");

            const string Module3 = "module3";
            var identity3 = new Identity(Module3, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            var identityManager = Mock.Of<IIdentityManager>(
                m =>
                    m.GetIdentities() == Task.FromResult(new List<Identity>() { identity2, identity3 }.AsEnumerable()) &&
                    m.CreateIdentityAsync(Module1, It.IsAny<string>()) == Task.FromResult(identity1) &&
                    m.DeleteIdentityAsync(Module3) == Task.FromResult(identity3));

            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri, enableOrphanedIdentityCleanup);
            var envVar = new Dictionary<string, EnvVal>();
            var desiredModule = new TestModule(Module1, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var currentModule1 = new TestModule(Module2, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var currentModule2 = new TestModule(Module3, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Unit]
        public async Task TestGetModulesIdentity_WithUnchanged_ShouldReturnAllWhenRequested(bool enableOrphanedIdentityCleanup)
        {
            // Arrange
            const string Module1 = "module1";
            var identity1 = new Identity(Module1, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            const string Module2 = "module2";
            var identity2 = new Identity(Module2, Guid.NewGuid().ToString(), "Me");

            const string Module3 = "module3";
            var identity3 = new Identity(Module3, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            var identityManager = Mock.Of<IIdentityManager>(
                m =>
                    m.GetIdentities() == Task.FromResult(new List<Identity>() { identity2, identity3 }.AsEnumerable()) &&
                    m.CreateIdentityAsync(Module1, It.IsAny<string>()) == Task.FromResult(identity1) &&
                    m.DeleteIdentityAsync(Module3) == Task.FromResult(identity3));

            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri, enableOrphanedIdentityCleanup);
            var envVar = new Dictionary<string, EnvVal>();
            var desiredModule = new TestModule(Module1, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var currentModule1 = new TestModule(Module2, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var currentModule2 = new TestModule(Module3, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            ModuleSet desired = ModuleSet.Create(new IModule[] { currentModule1, desiredModule });
            ModuleSet current = ModuleSet.Create(new IModule[] { currentModule1, currentModule2 });

            // Act
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desired, current);

            // Assert
            Assert.NotNull(moduleIdentities);
            Assert.True(moduleIdentities.TryGetValue(Module1, out IModuleIdentity module1Identity));
            Assert.True(moduleIdentities.TryGetValue(Module2, out IModuleIdentity module2Identity));
            Assert.Equal(Module1, module1Identity.ModuleId);
            Assert.Equal(Module2, module2Identity.ModuleId);
            Assert.IsType<IdentityProviderServiceCredentials>(module1Identity.Credentials);
            Assert.Equal(EdgeletUri.ToString(), ((IdentityProviderServiceCredentials)module1Identity.Credentials).ProviderUri);
            Assert.Equal(Option.None<string>(), ((IdentityProviderServiceCredentials)module1Identity.Credentials).Version);

            Mock.Get(identityManager).Verify(im => im.DeleteIdentityAsync(Module3));
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithOrphanedIdentities_ShouldRemoveThoseIdentities()
        {
            // Arrange
            const string Module1 = "module1";
            var identity1 = new Identity(Module1, Guid.NewGuid().ToString(), "Me");

            const string Module2 = "module2";
            var identity2 = new Identity(Module2, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            const string Module3 = "module3";
            var identity3 = new Identity(Module3, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            const string Module4 = "module4";
            var identity4 = new Identity(Module4, Guid.NewGuid().ToString(), "Me");

            const string Module5 = "module5";
            var identity5 = new Identity(Module5, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            var edgeAgentIdentity = new Identity(Constants.EdgeAgentModuleIdentityName, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            var edgeHubIdentity = new Identity(Constants.EdgeHubModuleIdentityName, Guid.NewGuid().ToString(), Constants.ModuleIdentityEdgeManagedByValue);

            var identityManager = Mock.Of<IIdentityManager>(
                m =>
                    m.GetIdentities() == Task.FromResult(new List<Identity>() { identity1, identity2, identity3, identity4, identity5, edgeAgentIdentity, edgeHubIdentity }.AsEnumerable()) &&
                    m.DeleteIdentityAsync(Module3) == Task.FromResult(identity3) &&
                    m.UpdateIdentityAsync(identity5.ModuleId, identity5.GenerationId, identity5.ManagedBy) == Task.FromResult(identity5));

            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri, true);
            var envVar = new Dictionary<string, EnvVal>();
            var currentModule1 = new TestModule(Module1, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var currentModule2 = new TestModule(Module2, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            var desiredModule = new TestModule(Module5, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, envVar);
            ModuleSet desired = ModuleSet.Create(new IModule[] { currentModule1, desiredModule });
            ModuleSet current = ModuleSet.Create(new IModule[] { currentModule1, currentModule2 }); // Module 3 didn't come up for some reason, but identity exists

            // Act
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desired, current);

            // Assert
            Assert.NotNull(moduleIdentities);

            Assert.True(moduleIdentities.TryGetValue(Module1, out IModuleIdentity module1Identity));
            Assert.False(moduleIdentities.TryGetValue(Module2, out IModuleIdentity module2Identity));
            Assert.False(moduleIdentities.TryGetValue(Module3, out IModuleIdentity module3Identity));
            Assert.True(moduleIdentities.TryGetValue(Module4, out IModuleIdentity module4Identity));
            Assert.True(moduleIdentities.TryGetValue(Module5, out IModuleIdentity module5Identity));
            Assert.Equal(Module1, module1Identity.ModuleId);
            Assert.Equal(Module4, module4Identity.ModuleId);
            Assert.Equal(Module5, module5Identity.ModuleId);

            Mock.Get(identityManager).Verify(im => im.GetIdentities());

            // Not tracked identity in current or desired
            Mock.Get(identityManager).Verify(im => im.DeleteIdentityAsync(Module3));
            // Not in desired
            Mock.Get(identityManager).Verify(im => im.DeleteIdentityAsync(Module2));
            // New in desired
            Mock.Get(identityManager).Verify(im => im.UpdateIdentityAsync(Module5, identity5.GenerationId, identity5.ManagedBy));

            Mock.Get(identityManager).VerifyNoOtherCalls();
        }
    }
}
