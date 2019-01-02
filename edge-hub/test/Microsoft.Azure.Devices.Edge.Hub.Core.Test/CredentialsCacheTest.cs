// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class CredentialsCacheTest
    {
        [Fact]
        public async Task RoundtripTest()
        {
            // Arrange
            var underlyingCredentialsCache = new NullCredentialsCache();
            var credentialsCache = new CredentialsCache(underlyingCredentialsCache);
            var identity1 = Mock.Of<IIdentity>(i => i.Id == "d1");
            var identity2 = Mock.Of<IIdentity>(i => i.Id == "d2/m2");
            var creds1 = Mock.Of<ITokenCredentials>(c => c.Identity == identity1);
            var creds2 = Mock.Of<IClientCredentials>(c => c.Identity == identity2);

            // Act
            await credentialsCache.Add(creds1);
            await credentialsCache.Add(creds2);

            Option<IClientCredentials> receivedClientCredentials1 = await credentialsCache.Get(identity1);
            Option<IClientCredentials> receivedClientCredentials2 = await credentialsCache.Get(identity2);

            // Assert
            Assert.True(receivedClientCredentials1.HasValue);
            Assert.True(receivedClientCredentials2.HasValue);
            Assert.Equal(creds1, receivedClientCredentials1.OrDefault());
            Assert.Equal(creds2, receivedClientCredentials2.OrDefault());
        }

        [Fact]
        public async Task GetFromPersistedCacheTest()
        {
            // Arrange            
            var identity1 = Mock.Of<IIdentity>(i => i.Id == "d1");
            var identity2 = Mock.Of<IIdentity>(i => i.Id == "d2/m2");
            var creds1 = Mock.Of<ITokenCredentials>(c => c.Identity == identity1);
            var creds2 = Mock.Of<IClientCredentials>(c => c.Identity == identity2);
            var underlyingCredentialsCache = new Mock<ICredentialsCache>();
            underlyingCredentialsCache.Setup(u => u.Get(identity1)).ReturnsAsync(Option.Some((IClientCredentials)creds1));
            underlyingCredentialsCache.Setup(u => u.Get(identity2)).ReturnsAsync(Option.Some(creds2));
            var credentialsCache = new CredentialsCache(underlyingCredentialsCache.Object);

            // Act
            Option<IClientCredentials> receivedClientCredentials1_1 = await credentialsCache.Get(identity1);
            Option<IClientCredentials> receivedClientCredentials2_1 = await credentialsCache.Get(identity2);
            Option<IClientCredentials> receivedClientCredentials1_2 = await credentialsCache.Get(identity1);
            Option<IClientCredentials> receivedClientCredentials2_2 = await credentialsCache.Get(identity2);

            // Assert
            Assert.True(receivedClientCredentials1_1.HasValue);
            Assert.True(receivedClientCredentials2_1.HasValue);
            Assert.Equal(creds1, receivedClientCredentials1_1.OrDefault());
            Assert.Equal(creds2, receivedClientCredentials2_1.OrDefault());

            Assert.True(receivedClientCredentials1_2.HasValue);
            Assert.True(receivedClientCredentials2_2.HasValue);
            Assert.Equal(creds1, receivedClientCredentials1_2.OrDefault());
            Assert.Equal(creds2, receivedClientCredentials2_2.OrDefault());

            underlyingCredentialsCache.Verify(u => u.Get(identity1), Times.Once);
            underlyingCredentialsCache.Verify(u => u.Get(identity2), Times.Once);
        }
    }
}
