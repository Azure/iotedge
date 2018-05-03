// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class ModuleIdentityLifecycleManagerTest
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithEmptyDiff_ShouldReturnEmptyIdentities()
        {
            var serviceClient = new Mock<IServiceClient>();
            string hostname = "hostname";
            string deviceId = "deviceId";
            string gatewayHostName = "localhost";
            var moduleConnectionStringBuilder = new ModuleConnectionString.ModuleConnectionStringBuilder(hostname, deviceId);

            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await new ModuleIdentityLifecycleManager(serviceClient.Object, moduleConnectionStringBuilder, gatewayHostName)
                .GetModuleIdentitiesAsync(ModuleSet.Empty, ModuleSet.Empty);

            Assert.True(modulesIdentities.Count() == 0);
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithUpdatedModules_NoServiceIdentity_ShouldCreateIdentities()
        {
            const string Name = "test-filters";

            var serviceClient = new Mock<IServiceClient>();
            string hostname = "hostname";
            string deviceId = "deviceId";
            string deviceSharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("primaryDeviceAccessKey"));
            string moduleSharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("primaryModuleAccessKey"));
            string gatewayHostName = "localhost";
            var moduleIdentityBuilder = new ModuleConnectionString.ModuleConnectionStringBuilder(hostname, deviceId);

            serviceClient.Setup(sc => sc.GetModules()).Returns(Task.FromResult(ImmutableList<Module>.Empty.AsEnumerable()));

            var createdModuleIdentity = new Module("device1", Name);
            createdModuleIdentity.Authentication = new AuthenticationMechanism();
            createdModuleIdentity.Authentication.Type = AuthenticationType.Sas;
            createdModuleIdentity.Authentication.SymmetricKey.PrimaryKey = moduleSharedAccessKey;
            Module[] updatedServiceIdentities = new[] { createdModuleIdentity};

            // If we change to IList Mock doesn't recognize and making it a non Lambda would add a lot of complexity on this code.
            // ReSharper disable PossibleMultipleEnumeration
            serviceClient.Setup(sc => sc.CreateModules(It.Is<IEnumerable<string>>(m => m.Count() == 1 && m.First() == Name))).Returns(Task.FromResult(updatedServiceIdentities));
            // ReSharper restore PossibleMultipleEnumeration

            var module = new TestModule(Name, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);

            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await new ModuleIdentityLifecycleManager(serviceClient.Object, moduleIdentityBuilder, gatewayHostName)
                .GetModuleIdentitiesAsync(ModuleSet.Create(new IModule[] { module }), ModuleSet.Empty);

            // If we change to IList Mock doesn't recognize and making it a non Lambda would add a lot of complexity on this code.
            // ReSharper disable PossibleMultipleEnumeration
            serviceClient.Verify(sc => sc.CreateModules(It.Is<IEnumerable<string>>(m => m.Count() == 1 && m.First() == Name)), Times.Once());
            // ReSharper restore PossibleMultipleEnumeration
            Assert.True(modulesIdentities.Count() == 1);
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithUpdatedModules_NoAccessKey_ShouldUpdateIdentities()
        {
            const string Name = "test-filters";

            var serviceModuleIdentity = new Module("device1", Name);
            serviceModuleIdentity.Authentication = new AuthenticationMechanism();
            serviceModuleIdentity.Authentication.Type = AuthenticationType.Sas;

            var serviceClient = new Mock<IServiceClient>();
            string hostname = "hostname";
            string deviceId = "deviceId";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("primarySymmetricKey"));
            string gatewayHostName = "localhost";
            var moduleIdentityBuilder = new ModuleConnectionString.ModuleConnectionStringBuilder(hostname, deviceId);

            Module[] serviceIdentities = new[] { serviceModuleIdentity };
            serviceClient.Setup(sc => sc.GetModules()).Returns(Task.FromResult(serviceIdentities.AsEnumerable()));
            serviceClient.Setup(sc => sc.UpdateModules(It.IsAny<IEnumerable<Module>>())).Callback(
                (IEnumerable<Module> modules) =>
                {
                    foreach (Module m in modules)
                    {
                        m.Authentication.SymmetricKey = new SymmetricKey();
                        m.Authentication.SymmetricKey.PrimaryKey = sharedAccessKey;
                    }
                }).Returns(Task.FromResult(serviceIdentities));

            var module = new TestModule(Name, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);

            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await new ModuleIdentityLifecycleManager(serviceClient.Object, moduleIdentityBuilder, gatewayHostName)
                .GetModuleIdentitiesAsync(ModuleSet.Create(new IModule[] { module }), ModuleSet.Empty);

            serviceClient.Verify(sc => sc.UpdateModules(It.IsAny<IEnumerable<Module>>()), Times.Once());
            Assert.True(modulesIdentities.Count() == 1);
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithUpdatedModules_AuthTypeNull_ShouldUpdateIdentities()
        {
            const string Name = "test-filters";

            var serviceModuleIdentity = new Module("device1", Name);
            serviceModuleIdentity.Authentication = null;

            var serviceClient = new Mock<IServiceClient>();
            string hostname = "hostname.fake.com";
            string deviceId = "deviceId";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("primarySymmetricKey"));
            string gatewayHostName = "localhost";
            var moduleIdentityBuilder = new ModuleConnectionString.ModuleConnectionStringBuilder(hostname, deviceId);

            Module[] serviceIdentities = new[] { serviceModuleIdentity };
            serviceClient.Setup(sc => sc.GetModules()).Returns(Task.FromResult(serviceIdentities.AsEnumerable()));
            serviceClient.Setup(sc => sc.UpdateModules(It.IsAny<IEnumerable<Module>>())).Callback(
                (IEnumerable<Module> modules) =>
                {
                    foreach (Module m in modules)
                    {
                        m.Authentication.SymmetricKey = new SymmetricKey();
                        m.Authentication.SymmetricKey.PrimaryKey = sharedAccessKey;
                    }
                }).Returns(Task.FromResult(serviceIdentities));

            var module = new TestModule(Name, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);

            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await new ModuleIdentityLifecycleManager(serviceClient.Object, moduleIdentityBuilder, gatewayHostName)
                .GetModuleIdentitiesAsync(ModuleSet.Create(new IModule[] { module }), ModuleSet.Empty);

            serviceClient.Verify(sc => sc.UpdateModules(It.IsAny<IEnumerable<Module>>()), Times.Once());
            Assert.True(modulesIdentities.Count() == 1);
            var creds = modulesIdentities.First().Value.Credentials as ConnectionStringCredentials;
            Assert.NotNull(creds);
            IotHubConnectionStringBuilder connectionString = IotHubConnectionStringBuilder.Create(creds.ConnectionString);
            Assert.NotNull(connectionString.SharedAccessKey);
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithUpdatedModules_AuthTypeNotSas_ShouldUpdateIdentities()
        {
            const string Name = "test-filters";
            string primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("primarySymmetricKey"));
            string secondaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("secondarySymmetricKey"));

            var serviceModuleIdentity = new Module("device1", Name);
            serviceModuleIdentity.Authentication = new AuthenticationMechanism();
            serviceModuleIdentity.Authentication.Type = AuthenticationType.CertificateAuthority;
            var thumbprint = new X509Thumbprint();
            thumbprint.PrimaryThumbprint = primaryKey;
            thumbprint.SecondaryThumbprint = secondaryKey;

            serviceModuleIdentity.Authentication.X509Thumbprint = thumbprint;

            var serviceClient = new Mock<IServiceClient>();
            string hostname = "hostname.fake.com";
            string deviceId = "deviceId";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string gatewayHostName = "localhost";
            var moduleConnectionStringBuilder = new ModuleConnectionString.ModuleConnectionStringBuilder(hostname, deviceId);

            Module[] serviceIdentities = new[] { serviceModuleIdentity };
            serviceClient.Setup(sc => sc.GetModules()).Returns(Task.FromResult(serviceIdentities.AsEnumerable()));
            serviceClient.Setup(sc => sc.UpdateModules(It.IsAny<IEnumerable<Module>>())).Callback(
                (IEnumerable<Module> modules) =>
                {
                    foreach (Module m in modules)
                    {
                        m.Authentication.SymmetricKey = new SymmetricKey();
                        m.Authentication.SymmetricKey.PrimaryKey = primaryKey;
                    }
                }).Returns(Task.FromResult(serviceIdentities));

            var module = new TestModule(Name, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);

            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await new ModuleIdentityLifecycleManager(serviceClient.Object, moduleConnectionStringBuilder, gatewayHostName)
                .GetModuleIdentitiesAsync(ModuleSet.Create(new IModule[] { module }), ModuleSet.Empty);

            serviceClient.Verify(sc => sc.UpdateModules(It.IsAny<IEnumerable<Module>>()), Times.Once());
            Assert.True(modulesIdentities.Count() == 1);
            var creds = modulesIdentities.First().Value.Credentials as ConnectionStringCredentials;
            Assert.NotNull(creds);
            IotHubConnectionStringBuilder connectionString = IotHubConnectionStringBuilder.Create(creds.ConnectionString);
            Assert.NotNull(connectionString.SharedAccessKey);
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithUpdatedModules_SymmKeyNull_ShouldUpdateIdentities()
        {
            const string Name = "test-filters";

            var serviceModuleIdentity = new Module("device1", Name);
            serviceModuleIdentity.Authentication = new AuthenticationMechanism();
            serviceModuleIdentity.Authentication.Type = AuthenticationType.Sas;
            serviceModuleIdentity.Authentication.SymmetricKey = null;

            var serviceClient = new Mock<IServiceClient>();
            string hostname = "hostname.fake.com";
            string deviceId = "deviceId";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("primaryAccessKey"));
            string gatewayHostName = "localhost";
            var moduleConnectionStringBuilder = new ModuleConnectionString.ModuleConnectionStringBuilder(hostname, deviceId);

            Module[] serviceIdentities = new[] { serviceModuleIdentity };
            serviceClient.Setup(sc => sc.GetModules()).Returns(Task.FromResult(serviceIdentities.AsEnumerable()));
            serviceClient.Setup(sc => sc.UpdateModules(It.IsAny<IEnumerable<Module>>())).Callback(
                (IEnumerable<Module> modules) =>
                {
                    foreach (Module m in modules)
                    {
                        m.Authentication.SymmetricKey = new SymmetricKey();
                        m.Authentication.SymmetricKey.PrimaryKey = sharedAccessKey;
                    }
                }).Returns(Task.FromResult(serviceIdentities));

            var module = new TestModule(Name, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);

            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await new ModuleIdentityLifecycleManager(serviceClient.Object, moduleConnectionStringBuilder, gatewayHostName)
                .GetModuleIdentitiesAsync(ModuleSet.Create(new IModule[] { module }), ModuleSet.Empty);

            serviceClient.Verify(sc => sc.UpdateModules(It.IsAny<IEnumerable<Module>>()), Times.Once());
            Assert.True(modulesIdentities.Count() == 1);
            var creds = modulesIdentities.First().Value.Credentials as ConnectionStringCredentials;
            Assert.NotNull(creds);
            IotHubConnectionStringBuilder connectionString = IotHubConnectionStringBuilder.Create(creds.ConnectionString);
            Assert.NotNull(connectionString.SharedAccessKey);
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithUpdatedModules_HasAccessKey_ShouldNotUpdate()
        {
            const string Name = "test-filters";

            var serviceModuleIdentity = new Module("device1", Name);
            serviceModuleIdentity.Authentication = new AuthenticationMechanism();
            var symmetricKey = new SymmetricKey();
            symmetricKey.PrimaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("primarySymmetricKey"));
            symmetricKey.SecondaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("secondarySymmetricKey"));

            serviceModuleIdentity.Authentication.SymmetricKey = symmetricKey;

            var serviceClient = new Mock<IServiceClient>();
            string hostname = "hostname";
            string deviceId = "deviceId";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string gatewayHostName = "localhost";
            var moduleConnectionStringBuilder = new ModuleConnectionString.ModuleConnectionStringBuilder(hostname, deviceId);

            Module[] serviceIdentities = new[] { serviceModuleIdentity };
            serviceClient.Setup(sc => sc.GetModules()).Returns(Task.FromResult(serviceIdentities.AsEnumerable()));
            serviceClient.Setup(sc => sc.CreateModules(It.Is<IEnumerable<string>>(m => m.Count() == 0))).Returns(Task.FromResult(new Module[0]));
            serviceClient.Setup(sc => sc.UpdateModules(It.Is<IEnumerable<Module>>(m => m.Count() == 0))).Returns(Task.FromResult(new Module[0]));

            var module = new TestModule(Name, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);

            IImmutableDictionary<string, IModuleIdentity> modulesIdentities = await new ModuleIdentityLifecycleManager(serviceClient.Object, moduleConnectionStringBuilder, gatewayHostName)
                .GetModuleIdentitiesAsync(ModuleSet.Create(new IModule[] { module }), ModuleSet.Empty);

            serviceClient.Verify(sc => sc.CreateModules(It.Is<IEnumerable<string>>(m => m.Count() == 0)), Times.Once);
            serviceClient.Verify(sc => sc.UpdateModules(It.Is<IEnumerable<Module>>(m => m.Count() == 0)), Times.Once);
            Assert.True(modulesIdentities.Count() == 1);
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithRemovedModules_ShouldRemove()
        {
            const string Name = "test-filters";
            // Use Json to create module because managedBy property can't has private set on Module object
            const string ModuleJson = "{\"moduleId\":\"test-filters\",\"deviceId\":\"device1\",\"authentication\":{\"symmetricKey\":{\"primaryKey\":\"cHJpbWFyeVN5bW1ldHJpY0tleQ == \",\"secondaryKey\":\"c2Vjb25kYXJ5U3ltbWV0cmljS2V5\"},\"x509Thumbprint\":{\"primaryThumbprint\":null,\"secondaryThumbprint\":null},\"type\":\"sas\"},\"managedBy\":\"iotEdge\"}";
            var serviceModuleIdentity = JsonConvert.DeserializeObject<Module>(ModuleJson);
            var currentModule = new TestModule(Name, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);

            var serviceClient = new Mock<IServiceClient>();
            string hostname = "hostname";
            string deviceId = "deviceId";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string gatewayHostName = "localhost";
            var moduleConnectionStringBuilder = new ModuleConnectionString.ModuleConnectionStringBuilder(hostname, deviceId);

            var serviceIdentities = new List<Microsoft.Azure.Devices.Module>();
            serviceIdentities.Add(serviceModuleIdentity);
            serviceClient.Setup(sc => sc.GetModules()).Returns(Task.FromResult(serviceIdentities.AsEnumerable()));
            // If we change to IList Mock doesn't recognize and making it a non Lambda would add a lot of complexity on this code.
            // ReSharper disable PossibleMultipleEnumeration
            serviceClient.Setup(sc => sc.RemoveModules(It.Is<IEnumerable<string>>(m => m.Count() == 1 && m.First() == Name))).Returns(Task.FromResult(ImmutableList<Module>.Empty.AsEnumerable()));
            // ReSharper restore PossibleMultipleEnumeration

            await new ModuleIdentityLifecycleManager(serviceClient.Object, moduleConnectionStringBuilder, gatewayHostName)
                .GetModuleIdentitiesAsync(ModuleSet.Empty, ModuleSet.Create(new IModule[] { currentModule }));

            // If we change to IList Mock doesn't recognize and making it a non Lambda would add a lot of complexity on this code.
            // ReSharper disable PossibleMultipleEnumeration
            serviceClient.Verify(sc => sc.RemoveModules(It.Is<IEnumerable<string>>(m => m.Count() == 1 && m.First() == Name)), Times.Once);
            // ReSharper restore PossibleMultipleEnumeration
        }

        [Fact]
        [Unit]
        public async Task TestGetModulesIdentity_WithRemovedModules_NotEdgeHubManaged_ShouldNotRemove()
        {
            const string Name = "test-filters";
            // Use Json to create module because managedBy property can't has private set on Module object
            const string ModuleJson = "{\"moduleId\":\"test-filters\",\"deviceId\":\"device1\",\"authentication\":{\"symmetricKey\":{\"primaryKey\":\"cHJpbWFyeVN5bW1ldHJpY0tleQ == \",\"secondaryKey\":\"c2Vjb25kYXJ5U3ltbWV0cmljS2V5\"},\"x509Thumbprint\":{\"primaryThumbprint\":null,\"secondaryThumbprint\":null},\"type\":\"sas\"},\"managedBy\":null}";
            var serviceModuleIdentity = JsonConvert.DeserializeObject<Module>(ModuleJson);
            var currentModule = new TestModule(Name, "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);

            var serviceClient = new Mock<IServiceClient>();
            string hostname = "hostname";
            string deviceId = "deviceId";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string gatewayHostName = "localhost";
            var moduleConnectionStringBuilder = new ModuleConnectionString.ModuleConnectionStringBuilder(hostname, deviceId);

            var serviceIdentities = new List<Microsoft.Azure.Devices.Module>();
            serviceIdentities.Add(serviceModuleIdentity);
            serviceClient.Setup(sc => sc.GetModules()).Returns(Task.FromResult(serviceIdentities.AsEnumerable()));
            serviceClient.Setup(sc => sc.RemoveModules(It.IsAny<IEnumerable<string>>())).Returns(Task.FromResult(ImmutableList<Module>.Empty.AsEnumerable()));

            await new ModuleIdentityLifecycleManager(serviceClient.Object, moduleConnectionStringBuilder, gatewayHostName)
                .GetModuleIdentitiesAsync(ModuleSet.Empty, ModuleSet.Create(new IModule[] { currentModule }));

            serviceClient.Verify(sc => sc.RemoveModules(It.Is<IEnumerable<string>>(m => m.Count() == 0)), Times.Once);
        }
    }
}
