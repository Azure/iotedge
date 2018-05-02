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
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;
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
        const string EdgeletUri = "localhost";
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
            Assert.True(modulesIdentities.Count() == 0);
            Mock.Get(identityManager).Verify();
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithUpdatedModules_ShouldCreateIdentities()
        {
            // Arrange
            const string Name = "module1";
            var identity = new Identity
            {
                ModuleId = Name,
                ManagedBy = "IotEdge",
                GenerationId = Guid.NewGuid().ToString()
            };

            var identityManager = Mock.Of<IIdentityManager>(m =>
                m.GetIdentities() == Task.FromResult(Enumerable.Empty<Identity>()) &&
                m.CreateIdentityAsync(Name) == Task.FromResult(identity));

            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri);
            var module = new TestModule(Name, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);

            // Act
            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(ModuleSet.Create(new IModule[] { module }), ModuleSet.Empty);

            // Assert
            Assert.True(modulesIdentities.Count() == 1);
            Assert.True(modulesIdentities.TryGetValue(Name, out IModuleIdentity moduleIdentity));
            Assert.Equal(moduleIdentity.ModuleId, Name);
            Assert.IsType<IdentityProviderServiceCredentials>(moduleIdentity.Credentials);
            Assert.Equal(EdgeletUri, ((IdentityProviderServiceCredentials)moduleIdentity.Credentials).ProviderUri);
            Assert.Equal(Option.None<string>(), ((IdentityProviderServiceCredentials)moduleIdentity.Credentials).Version);
            Mock.Get(identityManager).Verify();
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithRemovedModules_ShouldRemove()
        {
            // Arrange
            const string Module1 = "module1";
            var identity1 = new Identity
            {
                ModuleId = Module1,
                ManagedBy = "IotEdge",
                GenerationId = Guid.NewGuid().ToString()
            };

            const string Module2 = "module2";
            var identity2 = new Identity
            {
                ModuleId = Module2,
                ManagedBy = "Me",
                GenerationId = Guid.NewGuid().ToString()
            };

            const string Module3 = "module3";
            var identity3 = new Identity
            {
                ModuleId = Module3,
                ManagedBy = "IotEdge",
                GenerationId = Guid.NewGuid().ToString()
            };

            var identityManager = Mock.Of<IIdentityManager>(m =>
                m.GetIdentities() == Task.FromResult(new List<Identity>() { identity2, identity3 }.AsEnumerable()) &&
                m.CreateIdentityAsync(Module1) == Task.FromResult(identity1) &&
                m.DeleteIdentityAsync(Module3) == Task.FromResult(identity3));

            var moduleIdentityLifecycleManager = new ModuleIdentityLifecycleManager(identityManager, ModuleIdentityProviderServiceBuilder, EdgeletUri);
            var desiredModule = new TestModule(Module1, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);
            var currentModule1 = new TestModule(Module2, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);
            var currentModule2 = new TestModule(Module3, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);
            ModuleSet desired = ModuleSet.Create(new IModule[] { desiredModule });
            ModuleSet current = ModuleSet.Create(new IModule[] { currentModule1, currentModule2 });

            // Act
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = await moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desired, current);

            // Assert
            Assert.NotNull(moduleIdentities);
            Assert.True(moduleIdentities.TryGetValue(Module1, out IModuleIdentity module1Identity));
            Assert.Equal(Module1, module1Identity.ModuleId);
            Assert.IsType<IdentityProviderServiceCredentials>(module1Identity.Credentials);
            Assert.Equal(EdgeletUri, ((IdentityProviderServiceCredentials)module1Identity.Credentials).ProviderUri);
            Assert.Equal(Option.None<string>(), ((IdentityProviderServiceCredentials)module1Identity.Credentials).Version);
            Mock.Get(identityManager).Verify();
        }
    }
}
