// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ConnectionReauthenticatorTest
    {
        const string IoTHubHostName = "testhub.azure-devices.net";

        [Fact]
        public async Task TestConnectionReauthentication()
        {
            // Arrange
            var connectionManager = new Mock<IConnectionManager>(MockBehavior.Strict);
            var authenticator = new Mock<IAuthenticator>(MockBehavior.Strict);
            var credentialsStore = new Mock<ICredentialsCache>(MockBehavior.Strict);
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();
            TimeSpan reauthFrequency = TimeSpan.FromSeconds(3);

            var deviceIdentity = new DeviceIdentity(IoTHubHostName, "d1");
            var moduleIdentity = new ModuleIdentity(IoTHubHostName, "d1", "m1");
            var clients = new List<IIdentity>
            {
                deviceIdentity,
                moduleIdentity
            };
            connectionManager.Setup(c => c.GetConnectedClients()).Returns(clients);

            var deviceCredentials = Mock.Of<IClientCredentials>(c => c.Identity == deviceIdentity && c.AuthenticationType == AuthenticationType.SasKey);
            var moduleCredentials = Mock.Of<IClientCredentials>(c => c.Identity == moduleIdentity && c.AuthenticationType == AuthenticationType.SasKey);
            credentialsStore.Setup(c => c.Get(deviceIdentity)).ReturnsAsync(Option.Some(deviceCredentials));
            credentialsStore.Setup(c => c.Get(moduleIdentity)).ReturnsAsync(Option.Some(moduleCredentials));

            authenticator.Setup(a => a.ReauthenticateAsync(deviceCredentials)).ReturnsAsync(true);
            authenticator.Setup(a => a.ReauthenticateAsync(moduleCredentials)).ReturnsAsync(true);

            // Act
            var connectionReauthenticator = new ConnectionReauthenticator(connectionManager.Object, authenticator.Object, credentialsStore.Object, deviceScopeIdentitiesCache, reauthFrequency, Mock.Of<IIdentity>());
            connectionReauthenticator.Init();

            // Assert            
            connectionManager.Verify(c => c.GetConnectedClients(), Times.Never);

            await Task.Delay(reauthFrequency + TimeSpan.FromSeconds(1));
            connectionManager.Verify(c => c.GetConnectedClients(), Times.Once);
            authenticator.VerifyAll();
            credentialsStore.VerifyAll();
            Mock.Get(deviceScopeIdentitiesCache).VerifyAll();
        }

        [Fact]
        public async Task TestConnectionReauthentication_AuthenticationFailureTest()
        {
            // Arrange
            var connectionManager = new Mock<IConnectionManager>(MockBehavior.Strict);
            var authenticator = new Mock<IAuthenticator>(MockBehavior.Strict);
            var credentialsStore = new Mock<ICredentialsCache>(MockBehavior.Strict);
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();
            TimeSpan reauthFrequency = TimeSpan.FromSeconds(3);

            var deviceIdentity = new DeviceIdentity(IoTHubHostName, "d1");
            var moduleIdentity = new ModuleIdentity(IoTHubHostName, "d1", "m1");
            var clients = new List<IIdentity>
            {
                deviceIdentity,
                moduleIdentity
            };
            connectionManager.Setup(c => c.GetConnectedClients()).Returns(clients);
            connectionManager.Setup(c => c.RemoveDeviceConnection("d1")).Returns(Task.CompletedTask);
            connectionManager.Setup(c => c.RemoveDeviceConnection("d1/m1")).Returns(Task.CompletedTask);

            var deviceCredentials = Mock.Of<IClientCredentials>(c => c.Identity == deviceIdentity && c.AuthenticationType == AuthenticationType.SasKey);
            var moduleCredentials = Mock.Of<IClientCredentials>(c => c.Identity == moduleIdentity && c.AuthenticationType == AuthenticationType.SasKey);
            credentialsStore.Setup(c => c.Get(deviceIdentity)).ReturnsAsync(Option.Some(deviceCredentials));
            credentialsStore.Setup(c => c.Get(moduleIdentity)).ReturnsAsync(Option.Some(moduleCredentials));

            authenticator.Setup(a => a.ReauthenticateAsync(deviceCredentials)).ReturnsAsync(false);
            authenticator.Setup(a => a.ReauthenticateAsync(moduleCredentials)).ReturnsAsync(false);

            // Act
            var connectionReauthenticator = new ConnectionReauthenticator(connectionManager.Object, authenticator.Object, credentialsStore.Object, deviceScopeIdentitiesCache, reauthFrequency, Mock.Of<IIdentity>());
            connectionReauthenticator.Init();

            // Assert            
            connectionManager.Verify(c => c.GetConnectedClients(), Times.Never);

            await Task.Delay(reauthFrequency + TimeSpan.FromSeconds(1));
            connectionManager.Verify(c => c.GetConnectedClients(), Times.Once);
            connectionManager.VerifyAll();
            authenticator.VerifyAll();
            credentialsStore.VerifyAll();
            Mock.Get(deviceScopeIdentitiesCache).VerifyAll();
        }

        [Fact]
        public async Task TestConnectionReauthentication_MultipleLoopsTest()
        {
            // Arrange
            var connectionManager = new Mock<IConnectionManager>(MockBehavior.Strict);
            var authenticator = new Mock<IAuthenticator>(MockBehavior.Strict);
            var credentialsStore = new Mock<ICredentialsCache>(MockBehavior.Strict);
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();
            TimeSpan reauthFrequency = TimeSpan.FromSeconds(5);

            var deviceIdentity = new DeviceIdentity(IoTHubHostName, "d1");
            var moduleIdentity = new ModuleIdentity(IoTHubHostName, "d1", "m1");
            var clients = new List<IIdentity>
            {
                deviceIdentity,
                moduleIdentity
            };
            connectionManager.Setup(c => c.GetConnectedClients()).Returns(clients);
            connectionManager.Setup(c => c.RemoveDeviceConnection("d1")).Returns(Task.CompletedTask);

            var deviceCredentials = Mock.Of<IClientCredentials>(c => c.Identity == deviceIdentity && c.AuthenticationType == AuthenticationType.SasKey);
            var moduleCredentials = Mock.Of<IClientCredentials>(c => c.Identity == moduleIdentity && c.AuthenticationType == AuthenticationType.SasKey);
            credentialsStore.Setup(c => c.Get(deviceIdentity)).ReturnsAsync(Option.Some(deviceCredentials));
            credentialsStore.Setup(c => c.Get(moduleIdentity)).ReturnsAsync(Option.Some(moduleCredentials));

            authenticator.SetupSequence(a => a.ReauthenticateAsync(deviceCredentials))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            authenticator.SetupSequence(a => a.ReauthenticateAsync(moduleCredentials))
                .ReturnsAsync(true)
                .ReturnsAsync(true);

            // Act
            var connectionReauthenticator = new ConnectionReauthenticator(connectionManager.Object, authenticator.Object, credentialsStore.Object, deviceScopeIdentitiesCache, reauthFrequency, Mock.Of<IIdentity>());
            connectionReauthenticator.Init();

            // Assert            
            connectionManager.Verify(c => c.GetConnectedClients(), Times.Never);

            await Task.Delay(reauthFrequency * 2 + TimeSpan.FromSeconds(1));
            connectionManager.Verify(c => c.GetConnectedClients(), Times.Exactly(2));
            connectionManager.VerifyAll();
            authenticator.VerifyAll();
            credentialsStore.VerifyAll();
            Mock.Get(deviceScopeIdentitiesCache).VerifyAll();
        }

        [Fact]
        public void TestHandleServiceIdentityRemove()
        {
            // Arrange
            var connectionManager = new Mock<IConnectionManager>(MockBehavior.Strict);
            var authenticator = new Mock<IAuthenticator>(MockBehavior.Strict);
            var credentialsStore = new Mock<ICredentialsCache>(MockBehavior.Strict);
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            TimeSpan reauthFrequency = TimeSpan.FromSeconds(3);

            connectionManager.Setup(c => c.RemoveDeviceConnection("d1")).Returns(Task.CompletedTask);
            connectionManager.Setup(c => c.RemoveDeviceConnection("d1/m1")).Returns(Task.CompletedTask);

            // Act
            var connectionReauthenticator = new ConnectionReauthenticator(connectionManager.Object, authenticator.Object, credentialsStore.Object, deviceScopeIdentitiesCache.Object, reauthFrequency, Mock.Of<IIdentity>());
            deviceScopeIdentitiesCache.Raise(d => d.ServiceIdentityRemoved += null, null, "d1");
            deviceScopeIdentitiesCache.Raise(d => d.ServiceIdentityRemoved += null, null, "d1/m1");

            // Assert            
            Assert.NotNull(connectionReauthenticator);
            connectionManager.Verify(c => c.RemoveDeviceConnection("d1"), Times.Once);
            connectionManager.Verify(c => c.RemoveDeviceConnection("d1/m1"), Times.Once);
            connectionManager.VerifyAll();
            authenticator.VerifyAll();
            credentialsStore.VerifyAll();
            deviceScopeIdentitiesCache.VerifyAll();
        }

        [Fact]
        public void TestHandleServiceIdentityUpdateFailure()
        {
            // Arrange
            var connectionManager = new Mock<IConnectionManager>(MockBehavior.Strict);
            var authenticator = new Mock<IAuthenticator>(MockBehavior.Strict);
            var credentialsStore = new Mock<ICredentialsCache>(MockBehavior.Strict);
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            TimeSpan reauthFrequency = TimeSpan.FromSeconds(3);

            var deviceIdentity = new DeviceIdentity(IoTHubHostName, "d1");
            var moduleIdentity = new ModuleIdentity(IoTHubHostName, "d1", "m1");

            var deviceCredentials = Mock.Of<IClientCredentials>(c => c.Identity == deviceIdentity && c.AuthenticationType == AuthenticationType.SasKey);
            var moduleCredentials = Mock.Of<IClientCredentials>(c => c.Identity == moduleIdentity && c.AuthenticationType == AuthenticationType.SasKey);
            credentialsStore.Setup(c => c.Get(deviceIdentity)).ReturnsAsync(Option.Some(deviceCredentials));
            credentialsStore.Setup(c => c.Get(moduleIdentity)).ReturnsAsync(Option.Some(moduleCredentials));

            var deviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive && d.Identity == deviceIdentity);
            var moduleProxy = Mock.Of<IDeviceProxy>(d => d.IsActive && d.Identity == moduleIdentity);
            connectionManager.Setup(c => c.GetDeviceConnection("d1")).Returns(Option.Some(deviceProxy));
            connectionManager.Setup(c => c.GetDeviceConnection("d1/m1")).Returns(Option.Some(moduleProxy));

            connectionManager.Setup(c => c.RemoveDeviceConnection("d1")).Returns(Task.CompletedTask);
            connectionManager.Setup(c => c.RemoveDeviceConnection("d1/m1")).Returns(Task.CompletedTask);

            authenticator.Setup(a => a.ReauthenticateAsync(deviceCredentials)).ReturnsAsync(false);
            authenticator.Setup(a => a.ReauthenticateAsync(moduleCredentials)).ReturnsAsync(false);

            var serviceAuthentication = new ServiceAuthentication(ServiceAuthenticationType.None);
            var deviceServiceIdentity = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            var moduleServiceIdentity = new ServiceIdentity("d1/m1", "1234", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);

            // Act
            var connectionReauthenticator = new ConnectionReauthenticator(connectionManager.Object, authenticator.Object, credentialsStore.Object, deviceScopeIdentitiesCache.Object, reauthFrequency, Mock.Of<IIdentity>());
            deviceScopeIdentitiesCache.Raise(d => d.ServiceIdentityUpdated += null, null, deviceServiceIdentity);
            deviceScopeIdentitiesCache.Raise(d => d.ServiceIdentityUpdated += null, null, moduleServiceIdentity);

            // Assert            
            Assert.NotNull(connectionReauthenticator);
            connectionManager.Verify(c => c.RemoveDeviceConnection("d1"), Times.Once);
            connectionManager.Verify(c => c.RemoveDeviceConnection("d1/m1"), Times.Once);
            connectionManager.VerifyAll();
            authenticator.VerifyAll();
            credentialsStore.VerifyAll();
            deviceScopeIdentitiesCache.VerifyAll();
        }

        [Fact]
        public void TestHandleServiceIdentityUpdateSuccess()
        {
            // Arrange
            var connectionManager = new Mock<IConnectionManager>(MockBehavior.Strict);
            var authenticator = new Mock<IAuthenticator>(MockBehavior.Strict);
            var credentialsStore = new Mock<ICredentialsCache>(MockBehavior.Strict);
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            TimeSpan reauthFrequency = TimeSpan.FromSeconds(3);

            var deviceIdentity = new DeviceIdentity(IoTHubHostName, "d1");
            var moduleIdentity = new ModuleIdentity(IoTHubHostName, "d1", "m1");

            var deviceCredentials = Mock.Of<IClientCredentials>(c => c.Identity == deviceIdentity && c.AuthenticationType == AuthenticationType.SasKey);
            var moduleCredentials = Mock.Of<IClientCredentials>(c => c.Identity == moduleIdentity && c.AuthenticationType == AuthenticationType.SasKey);
            credentialsStore.Setup(c => c.Get(deviceIdentity)).ReturnsAsync(Option.Some(deviceCredentials));
            credentialsStore.Setup(c => c.Get(moduleIdentity)).ReturnsAsync(Option.Some(moduleCredentials));

            var deviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive && d.Identity == deviceIdentity);
            var moduleProxy = Mock.Of<IDeviceProxy>(d => d.IsActive && d.Identity == moduleIdentity);
            connectionManager.Setup(c => c.GetDeviceConnection("d1")).Returns(Option.Some(deviceProxy));
            connectionManager.Setup(c => c.GetDeviceConnection("d1/m1")).Returns(Option.Some(moduleProxy));

            connectionManager.Setup(c => c.RemoveDeviceConnection("d1")).Returns(Task.CompletedTask);
            connectionManager.Setup(c => c.RemoveDeviceConnection("d1/m1")).Returns(Task.CompletedTask);

            authenticator.Setup(a => a.ReauthenticateAsync(deviceCredentials)).ReturnsAsync(true);
            authenticator.Setup(a => a.ReauthenticateAsync(moduleCredentials)).ReturnsAsync(true);

            var serviceAuthentication = new ServiceAuthentication(ServiceAuthenticationType.None);
            var deviceServiceIdentity = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            var moduleServiceIdentity = new ServiceIdentity("d1/m1", "1234", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);

            // Act
            var connectionReauthenticator = new ConnectionReauthenticator(connectionManager.Object, authenticator.Object, credentialsStore.Object, deviceScopeIdentitiesCache.Object, reauthFrequency, Mock.Of<IIdentity>());
            deviceScopeIdentitiesCache.Raise(d => d.ServiceIdentityUpdated += null, null, deviceServiceIdentity);
            deviceScopeIdentitiesCache.Raise(d => d.ServiceIdentityUpdated += null, null, moduleServiceIdentity);

            // Assert            
            Assert.NotNull(connectionReauthenticator);
            connectionManager.Verify(c => c.RemoveDeviceConnection("d1"), Times.Never);
            connectionManager.Verify(c => c.RemoveDeviceConnection("d1/m1"), Times.Never);
            connectionManager.Verify(c => c.GetDeviceConnection("d1"), Times.Once);
            connectionManager.Verify(c => c.GetDeviceConnection("d1/m1"), Times.Once);
            authenticator.VerifyAll();
            credentialsStore.VerifyAll();
            deviceScopeIdentitiesCache.VerifyAll();
        }
    }
}
