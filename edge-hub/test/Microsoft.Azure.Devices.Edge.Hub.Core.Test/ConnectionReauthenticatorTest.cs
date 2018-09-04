// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
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
            var credentialsStore = new Mock<ICredentialsStore>(MockBehavior.Strict);
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
            var connectionReauthenticator = new ConnectionReauthenticator(connectionManager.Object, authenticator.Object, credentialsStore.Object, deviceScopeIdentitiesCache, reauthFrequency);
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
            var credentialsStore = new Mock<ICredentialsStore>(MockBehavior.Strict);
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
            var connectionReauthenticator = new ConnectionReauthenticator(connectionManager.Object, authenticator.Object, credentialsStore.Object, deviceScopeIdentitiesCache, reauthFrequency);
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
            var credentialsStore = new Mock<ICredentialsStore>(MockBehavior.Strict);
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
            var connectionReauthenticator = new ConnectionReauthenticator(connectionManager.Object, authenticator.Object, credentialsStore.Object, deviceScopeIdentitiesCache, reauthFrequency);
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
    }
}
