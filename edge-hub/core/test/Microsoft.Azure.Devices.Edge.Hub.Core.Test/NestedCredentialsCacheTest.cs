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
    public class NestedCredentialsCacheTest
    {
        [Fact]
        public async Task RoundtripTest()
        {
            // Arrange
            var underlyingCredentialsCache = new NullCredentialsCache();
            var credentialsCache = new NestedCredentialsCache(underlyingCredentialsCache);
            var identity1 = Mock.Of<IIdentity>(i => i.Id == "d1");
            var identity2 = Mock.Of<IIdentity>(i => i.Id == "d2/m2");
            var identity3 = Mock.Of<IIdentity>(i => i.Id == "d3");
            var identity4 = Mock.Of<IIdentity>(i => i.Id == "d4");
            var edgeHub = Mock.Of<IIdentity>(i => i.Id == "edge1/$edgeHub");
            var creds1 = Mock.Of<ITokenCredentials>(c => c.Identity == identity1);
            var creds2 = Mock.Of<IClientCredentials>(c => c.Identity == identity2);
            var creds3 = Mock.Of<IClientCredentials>(c => c.Identity == edgeHub && c.AuthChain == Option.Some("d3;edge1"));
            var creds4 = Mock.Of<IClientCredentials>(c => c.Identity == edgeHub && c.AuthChain == Option.Some("d4;edge1"));

            // Act
            await credentialsCache.Add(creds1);
            await credentialsCache.Add(creds2);
            await credentialsCache.Add(creds3);
            await credentialsCache.Add(creds4);

            Option<IClientCredentials> receivedClientCredentials1 = await credentialsCache.Get(identity1);
            Option<IClientCredentials> receivedClientCredentials2 = await credentialsCache.Get(identity2);
            Option<IClientCredentials> receivedClientCredentials3 = await credentialsCache.Get(identity3);
            Option<IClientCredentials> receivedClientCredentials4 = await credentialsCache.Get(identity4);

            // Assert
            Assert.True(receivedClientCredentials1.HasValue);
            Assert.True(receivedClientCredentials2.HasValue);
            Assert.True(receivedClientCredentials3.HasValue);
            Assert.True(receivedClientCredentials4.HasValue);
            Assert.Equal(creds1, receivedClientCredentials1.OrDefault());
            Assert.Equal(creds2, receivedClientCredentials2.OrDefault());
            Assert.Equal(creds3, receivedClientCredentials3.OrDefault());
            Assert.Equal(creds4, receivedClientCredentials4.OrDefault());
        }

        [Fact]
        public async Task GetFromPersistedCacheTest()
        {
            // Arrange
            var identity1 = Mock.Of<IIdentity>(i => i.Id == "d1");
            var identity2 = Mock.Of<IIdentity>(i => i.Id == "d2/m2");
            var identity3 = Mock.Of<IIdentity>(i => i.Id == "d3");
            var identity4 = Mock.Of<IIdentity>(i => i.Id == "d4");
            var edgeHub = Mock.Of<IIdentity>(i => i.Id == "edge1/$edgeHub");
            var creds1 = Mock.Of<ITokenCredentials>(c => c.Identity == identity1);
            var creds2 = Mock.Of<IClientCredentials>(c => c.Identity == identity2);
            var creds3 = Mock.Of<IClientCredentials>(c => c.Identity == edgeHub && c.AuthChain == Option.Some("d3;edge1"));
            var creds4 = Mock.Of<IClientCredentials>(c => c.Identity == edgeHub && c.AuthChain == Option.Some("d4;edge1"));
            var underlyingCredentialsCache = new Mock<ICredentialsCache>();
            underlyingCredentialsCache.Setup(u => u.Get(identity1)).ReturnsAsync(Option.Some((IClientCredentials)creds1));
            underlyingCredentialsCache.Setup(u => u.Get(identity2)).ReturnsAsync(Option.Some(creds2));
            underlyingCredentialsCache.Setup(u => u.Get(identity3)).ReturnsAsync(Option.Some(creds3));
            underlyingCredentialsCache.Setup(u => u.Get(identity4)).ReturnsAsync(Option.Some(creds4));
            var credentialsCache = new NestedCredentialsCache(underlyingCredentialsCache.Object);

            // Act
            Option<IClientCredentials> receivedClientCredentials1_1 = await credentialsCache.Get(identity1);
            Option<IClientCredentials> receivedClientCredentials2_1 = await credentialsCache.Get(identity2);
            Option<IClientCredentials> receivedClientCredentials1_2 = await credentialsCache.Get(identity1);
            Option<IClientCredentials> receivedClientCredentials2_2 = await credentialsCache.Get(identity2);
            Option<IClientCredentials> receivedClientCredentials3 = await credentialsCache.Get(identity3);
            Option<IClientCredentials> receivedClientCredentials4 = await credentialsCache.Get(identity4);

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

            Assert.True(receivedClientCredentials3.HasValue);
            Assert.True(receivedClientCredentials4.HasValue);
            Assert.Equal(creds3, receivedClientCredentials3.OrDefault());
            Assert.Equal(creds4, receivedClientCredentials4.OrDefault());
        }

        [Fact]
        public async Task OnBehalfOfCredentialsUpdateTest()
        {
            // Arrange
            var underlyingCredentialsCache = new NullCredentialsCache();
            var credentialsCache = new NestedCredentialsCache(underlyingCredentialsCache);
            var identity1 = Mock.Of<IIdentity>(i => i.Id == "d1");
            var identity2 = Mock.Of<IIdentity>(i => i.Id == "d2");
            var edgeHub = Mock.Of<IIdentity>(i => i.Id == "edge1/$edgeHub");
            var creds1 = new TokenCredentials(edgeHub, "edgeHubToken1", "productinfo1", Option.Some("modelId1"), Option.Some("d1;edge1"), true);
            var creds2 = new TokenCredentials(edgeHub, "edgeHubToken1", "productinfo2", Option.Some("modelId2"), Option.Some("d2;edge1"), true);
            var newActorCreds = new TokenCredentials(edgeHub, "edgeHubToken2", "productinfo3", Option.Some("modelId3"), Option.None<string>(), true);

            // Act
            await credentialsCache.Add(creds1);
            await credentialsCache.Add(creds2);
            await credentialsCache.Add(newActorCreds);

            Option<IClientCredentials> receivedClientCredentials1 = await credentialsCache.Get(identity1);
            Option<IClientCredentials> receivedClientCredentials2 = await credentialsCache.Get(identity2);

            // Assert
            Assert.True(receivedClientCredentials1.HasValue);
            Assert.True(receivedClientCredentials2.HasValue);

            var receivedToken1 = receivedClientCredentials1.OrDefault() as ITokenCredentials;
            Assert.Equal(newActorCreds.Identity, receivedToken1.Identity);
            Assert.Equal(newActorCreds.Token, receivedToken1.Token);
            Assert.Equal(creds1.AuthChain, receivedToken1.AuthChain);
            Assert.Equal(creds1.ProductInfo, receivedToken1.ProductInfo);
            Assert.Equal(creds1.ModelId, receivedToken1.ModelId);

            var receivedToken2 = receivedClientCredentials2.OrDefault() as ITokenCredentials;
            Assert.Equal(newActorCreds.Identity, receivedToken2.Identity);
            Assert.Equal(newActorCreds.Token, receivedToken2.Token);
            Assert.Equal(creds2.AuthChain, receivedToken2.AuthChain);
            Assert.Equal(creds2.ProductInfo, receivedToken2.ProductInfo);
            Assert.Equal(creds2.ModelId, receivedToken2.ModelId);
        }
    }
}
