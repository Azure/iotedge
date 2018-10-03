// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class TokenCredentialsStoreTest
    {
        [Fact]
        [Unit]
        public async Task RoundtripTokenCredentialsTest()
        {
            // Arrange
            string iothubHostName = "iothub1.azure.net";
            string callerProductInfo = "productInfo";
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/device1/modules/moduleId");
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var credentials = new TokenCredentials(identity, sasToken, callerProductInfo);

            var dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            var encryptedStore = new EncryptedStore<string, string>(storeProvider.GetEntityStore<string, string>("tokenCredentials"), new NullEncryptionProvider());
            var tokenCredentialsStore = new TokenCredentialsCache(encryptedStore);

            // Act
            await tokenCredentialsStore.Add(credentials);
            Option<IClientCredentials> storedCredentials = await tokenCredentialsStore.Get(identity);

            // Assert
            Assert.True(storedCredentials.HasValue);
            var storedTokenCredentials = storedCredentials.OrDefault() as ITokenCredentials;
            Assert.NotNull(storedTokenCredentials);
            Assert.Equal(sasToken, storedTokenCredentials.Token);
        }

        [Fact]
        [Unit]
        public async Task RoundtripTokenCredentialsWithEncryptionTest()
        {
            // Arrange
            string iothubHostName = "iothub1.azure.net";
            string callerProductInfo = "productInfo";
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/device1/modules/moduleId");
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var credentials = new TokenCredentials(identity, sasToken, callerProductInfo);

            var dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            var encryptedStore = new EncryptedStore<string, string>(storeProvider.GetEntityStore<string, string>("tokenCredentials"), new TestEncryptionProvider());
            var tokenCredentialsStore = new TokenCredentialsCache(encryptedStore);

            // Act
            await tokenCredentialsStore.Add(credentials);
            Option<IClientCredentials> storedCredentials = await tokenCredentialsStore.Get(identity);

            // Assert
            Assert.True(storedCredentials.HasValue);
            var storedTokenCredentials = storedCredentials.OrDefault() as ITokenCredentials;
            Assert.NotNull(storedTokenCredentials);
            Assert.Equal(sasToken, storedTokenCredentials.Token);
        }

        [Fact]
        [Unit]
        public async Task RoundtripNonTokenCredentialsTest()
        {
            // Arrange
            string callerProductInfo = "productInfo";
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var credentials = new X509CertCredentials(identity, callerProductInfo);

            var dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            var encryptedStore = new EncryptedStore<string, string>(storeProvider.GetEntityStore<string, string>("tokenCredentials"), new TestEncryptionProvider());
            var tokenCredentialsStore = new TokenCredentialsCache(encryptedStore);

            // Act
            await tokenCredentialsStore.Add(credentials);
            Option<IClientCredentials> storedCredentials = await tokenCredentialsStore.Get(identity);

            // Assert
            Assert.False(storedCredentials.HasValue);
        }

        class TestEncryptionProvider : IEncryptionProvider
        {
            public Task<string> DecryptAsync(string encryptedText) => Task.FromResult(Encoding.UTF8.GetString(Convert.FromBase64String(encryptedText)));

            public Task<string> EncryptAsync(string plainText) => Task.FromResult(Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText)));
        }
    }
}
