// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class TokenCacheAuthenticatorTest
    {
        [Unit]
        [Fact]
        public async Task AuthenticateWithIotHubTest()
        {
            // Arrange
            var cloudProxy = Mock.Of<ICloudProxy>(c => c.IsActive && c.OpenAsync() == Task.FromResult(true));
            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.CreateCloudConnectionAsync(It.IsAny<IClientCredentials>()) == Task.FromResult(Try.Success(cloudProxy)));

            var credentialsStore = new Mock<ICredentialsCache>();
            credentialsStore.Setup(c => c.Get(It.IsAny<IIdentity>()))
                .ReturnsAsync(Option.None<IClientCredentials>());
            credentialsStore.Setup(c => c.Add(It.IsAny<IClientCredentials>()))
                .Returns(Task.CompletedTask);

            string iothubHostName = "iothub1.azure.net";
            string callerProductInfo = "productInfo";
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/device1/modules/moduleId");
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var credentials = new TokenCredentials(identity, sasToken, callerProductInfo);

            var tokenCredentialsAuthenticator = new TokenCacheAuthenticator(new CloudTokenAuthenticator(connectionManager, iothubHostName), credentialsStore.Object, iothubHostName);

            // Act
            bool isAuthenticated = await tokenCredentialsAuthenticator.AuthenticateAsync(credentials);

            // assert
            Assert.True(isAuthenticated);
            Mock.Verify(credentialsStore);
            Mock.Verify(Mock.Get(connectionManager));
            Mock.Verify(Mock.Get(cloudProxy));
        }

        [Unit]
        [Fact]
        public async Task AuthenticateFromCacheTest()
        {
            // Arrange
            var cloudProxy = Mock.Of<ICloudProxy>(c => c.IsActive && c.OpenAsync() == Task.FromResult(true));
            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.CreateCloudConnectionAsync(It.IsAny<IClientCredentials>()) == Task.FromResult(Try.Success(cloudProxy)));            

            string iothubHostName = "iothub1.azure.net";
            string callerProductInfo = "productInfo";
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/device1/modules/moduleId");
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var credentials = new TokenCredentials(identity, sasToken, callerProductInfo);

            var storedTokenCredentials = Mock.Of<ITokenCredentials>(c => c.Token == sasToken);
            var credentialsStore = new Mock<ICredentialsCache>();
            credentialsStore.Setup(c => c.Get(It.IsAny<IIdentity>()))
                .ReturnsAsync(Option.Some((IClientCredentials)storedTokenCredentials));
            credentialsStore.Setup(c => c.Add(It.IsAny<IClientCredentials>()))
                .Returns(Task.CompletedTask);

            var tokenCredentialsAuthenticator = new TokenCacheAuthenticator(new CloudTokenAuthenticator(connectionManager, iothubHostName), credentialsStore.Object, iothubHostName);

            // Act
            bool isAuthenticated = await tokenCredentialsAuthenticator.AuthenticateAsync(credentials);

            // assert
            Assert.True(isAuthenticated);
            Mock.Verify(credentialsStore);
            Mock.Get(connectionManager).Verify(c => c.CreateCloudConnectionAsync(It.IsAny<IClientCredentials>()), Times.Never);
            Mock.Get(cloudProxy).Verify(c => c.OpenAsync(), Times.Never);
        }

        [Unit]
        [Fact]
        public async Task NotAuthenticatedTest()
        {
            // Arrange
            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.CreateCloudConnectionAsync(It.IsAny<IClientCredentials>()) == Task.FromResult(Try<ICloudProxy>.Failure(new UnauthorizedException("Not authorized"))));

            string iothubHostName = "iothub1.azure.net";
            string callerProductInfo = "productInfo";
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/device1/modules/moduleId");
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var credentials = new TokenCredentials(identity, sasToken, callerProductInfo);

            string sasToken2 = TokenHelper.CreateSasToken($"{iothubHostName}/devices/device1/modules/moduleId") + "a";
            var storedTokenCredentials = Mock.Of<ITokenCredentials>(c => c.Token == sasToken2);
            var credentialsStore = new Mock<ICredentialsCache>();
            credentialsStore.Setup(c => c.Get(It.IsAny<IIdentity>()))
                .ReturnsAsync(Option.Some((IClientCredentials)storedTokenCredentials));
            credentialsStore.Setup(c => c.Add(It.IsAny<IClientCredentials>()))
                .Returns(Task.CompletedTask);

            var tokenCredentialsAuthenticator = new TokenCacheAuthenticator(new CloudTokenAuthenticator(connectionManager, iothubHostName), credentialsStore.Object, iothubHostName);

            // Act
            bool isAuthenticated = await tokenCredentialsAuthenticator.AuthenticateAsync(credentials);

            // assert
            Assert.False(isAuthenticated);
            Mock.Verify(credentialsStore);
            Mock.Verify(Mock.Get(connectionManager));
        }

        [Unit]
        [Fact]
        public async Task CacheTokenExpiredNotAuthenticatedTest()
        {
            // Arrange
            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.CreateCloudConnectionAsync(It.IsAny<IClientCredentials>()) == Task.FromResult(Try<ICloudProxy>.Failure(new UnauthorizedException("Not authorized"))));

            string iothubHostName = "iothub1.azure.net";
            string callerProductInfo = "productInfo";
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/device1/modules/moduleId", expired: true);
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var credentials = new TokenCredentials(identity, sasToken, callerProductInfo);

            var storedTokenCredentials = Mock.Of<ITokenCredentials>(c => c.Token == sasToken);
            var credentialsStore = new Mock<ICredentialsCache>();
            credentialsStore.Setup(c => c.Get(It.IsAny<IIdentity>()))
                .ReturnsAsync(Option.Some((IClientCredentials)storedTokenCredentials));
            credentialsStore.Setup(c => c.Add(It.IsAny<IClientCredentials>()))
                .Returns(Task.CompletedTask);

            var tokenCredentialsAuthenticator = new TokenCacheAuthenticator(new CloudTokenAuthenticator(connectionManager, iothubHostName), credentialsStore.Object, iothubHostName);

            // Act
            bool isAuthenticated = await tokenCredentialsAuthenticator.AuthenticateAsync(credentials);

            // assert
            Assert.False(isAuthenticated);
            Mock.Verify(credentialsStore);
            Mock.Verify(Mock.Get(connectionManager));
        }
    }
}
