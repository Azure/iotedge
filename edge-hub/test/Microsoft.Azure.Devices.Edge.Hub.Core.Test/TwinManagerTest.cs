namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;
    using System.Linq;
    using System.Collections.Generic;

    [Unit]
    public class TwinManagerTest
    {
        Option<IEntityStore<string, TwinInfo>> twinStore;
        IMessageConverter<TwinCollection> twinCollectionMessageConverter;
        IMessageConverter<Twin> twinMessageConverter;

        public TwinManagerTest()
        {
            this.twinStore = Option.Some(new StoreProvider(new InMemoryDbStoreProvider()).GetEntityStore<string, TwinInfo>("default"));
            this.twinCollectionMessageConverter = new TwinCollectionMessageConverter();
            this.twinMessageConverter = new TwinMessageConverter();
        }

        [Fact]
        public void TwinManagerConstructorVerifiesArguments()
        {
            IConnectionManager connectionManager = Mock.Of<IConnectionManager>();
            Assert.Throws<ArgumentNullException>(() => new TwinManager(null, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore));
            Assert.Throws<ArgumentNullException>(() => new TwinManager(connectionManager, null, this.twinMessageConverter, this.twinStore));
            Assert.Throws<ArgumentNullException>(() => new TwinManager(connectionManager, this.twinCollectionMessageConverter, null, this.twinStore));
        }

        [Fact]
        public void TwinManagerConstructorWithValidArgumentsSucceeds()
        {
            IConnectionManager connectionManager = Mock.Of<IConnectionManager>();
            Assert.NotNull(new TwinManager(connectionManager, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore));
            Assert.NotNull(new TwinManager(connectionManager, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>()));
        }

        [Fact]
        public async void GetTwinWhenCloudOnlineTwinNotStoredSuccess()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device";

            bool storeHit = false;
            bool storeMiss = false;

            // Act
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

            // Assert
            Assert.Equal(storeHit, false);
            Assert.Equal(storeMiss, true);

            // Act
            IMessage received = await twinManager.GetTwinAsync(deviceId);

            // Assert
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));
            Assert.Equal(storeHit, true);
        }

        [Fact]
        public async void GetTwinWhenCloudOfflineSuccess()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device";
            await twinManager.GetTwinAsync(deviceId);

            bool getTwinCalled = false;
            bool storeHit = false;
            mockProxy.Setup(t => t.GetTwinAsync()).Callback(() => getTwinCalled = true).Throws(new Exception("Offline"));

            // Act
            IMessage received = await twinManager.GetTwinAsync(deviceId);

            // Assert
            Assert.Equal(getTwinCalled, true);
            Assert.Equal(received.Body, twinMessage.Body);
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));
            Assert.Equal(storeHit, true);
        }

        [Fact]
        public async void GetTwinPassthroughWhenTwinNotStoredSuccess()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>());

            string deviceId = "device";

            // Act
            IMessage received = await twinManager.GetTwinAsync(deviceId);

            // Assert
            Assert.Equal(received.Body, twinMessage.Body);
            Assert.Equal(twinManager.TwinStore, Option.None<IEntityStore<string, TwinInfo>>());
        }

        [Fact]
        public async void UpdateDesiredPropertiesWhenTwinStoredVersionPlus1Success()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            twin.Properties.Desired = new TwinCollection()
            {
                ["name"] = "original",
                ["$version"] = 32
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

            bool receivedCallback = false;
            Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedCallback = true)
                .Returns(Task.CompletedTask);
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device";
            IMessage received = await twinManager.GetTwinAsync(deviceId);

            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            // Act
            await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage);
            TwinInfo patched = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { patched = t; return Task.CompletedTask; }, () => Task.FromResult<TwinInfo>(null));

            // Assert
            Assert.Equal(patched.Twin.Properties.Desired.ToJson(), collection.ToJson());
            Assert.Equal(receivedCallback, true);
        }

        [Fact]
        public async void UpdateDesiredPropertiesWhenTwinNotStoredVersionPlus1Success()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            bool getTwinCalled = false;
            Mock<ICloudProxy> mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Callback(() => getTwinCalled = true).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

            bool receivedCallback = false;
            Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedCallback = true)
                .Returns(Task.CompletedTask);
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device";
            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 1
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);
            bool storeHit = false;

            // Act
            await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage);
            TwinInfo updated = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; updated = t; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));

            // Assert
            Assert.Equal(getTwinCalled, true);
            Assert.Equal(storeHit, true);
            Assert.Equal(updated.Twin.Properties.Desired, collection);
            Assert.Equal(receivedCallback, true);
        }

        [Fact]
        public async void UpdateDesiredPropertiesPassthroughSuccess()
        {
            // Arrange
            TwinCollection received = new TwinCollection();

            Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>((m) =>
                { received = this.twinCollectionMessageConverter.FromMessage(m); })
                .Returns(Task.CompletedTask);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            string deviceId = "device";
            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>());

            // Act
            await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage);

            // Assert
            Assert.Equal(received, collection);
            Assert.Equal(twinManager.TwinStore, Option.None<IEntityStore<string, TwinInfo>>());
        }

        [Fact]
        public async void UpdateReportedPropertiesPassthroughSuccess()
        {
            // Arrange
            IMessage receivedMessage = null;
            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>((m) => receivedMessage = m)
                .Returns(Task.CompletedTask);
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            string deviceId = "device";
            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>());

            // Act
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

            // Assert
            Assert.Equal(collectionMessage, receivedMessage);
            Assert.Equal(twinManager.TwinStore, Option.None<IEntityStore<string, TwinInfo>>());
        }

        [Fact]
        public async void UpdateReportedPropertiesWhenCloudOnlineTwinNotStoredSuccess()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            twin.Properties.Reported = new TwinCollection()
            {
                ["name"] = "oldvalue",
                ["$version"] = 32
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            IMessage receivedMessage = null;
            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>((m) => receivedMessage = m)
                .Returns(Task.CompletedTask);
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            string deviceId = "device";
            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            bool storeMiss = false;
            bool storeHit = false;

            // Act - find the twin
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

            // Assert - verify that the twin is not in the store
            Assert.Equal(storeHit, false);
            Assert.Equal(storeMiss, true);

            // Act
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

            // Assert - verify that the message was sent to the cloud proxy
            Assert.Equal(receivedMessage, collectionMessage);

            // Assert - verify that the twin was fetched
            storeMiss = false;
            storeHit = false;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });
            Assert.Equal(storeHit, true);

            // Assert - verify that the local twin's reported properties updated.
            // verify that the local patch of reported properties was not updated.
            TwinInfo retrieved = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { retrieved = t; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));
            Assert.Equal(storeMiss, false);
            Assert.True(JToken.DeepEquals(
                JsonConvert.DeserializeObject<JToken>(retrieved.Twin.Properties.Reported.ToJson()),
                JsonConvert.DeserializeObject<JToken>(collection.ToJson())));

            Assert.True(JToken.DeepEquals(
                JsonConvert.DeserializeObject<JToken>(retrieved.ReportedPropertiesPatch.ToJson()),
                JsonConvert.DeserializeObject<JToken>((new TwinCollection()).ToJson())));
        }

        [Fact]
        public async void UpdateReportedPropertiesWhenCloudOfflineTwinNotStoredSuccess()
        {
            // Arrange
            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            string deviceId = "device";
            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value"
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            bool storeMiss = false;
            bool storeHit = false;

            // Act
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

            // Assert
            Assert.Equal(storeHit, false);
            Assert.Equal(storeMiss, true);

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage));
        }

        [Fact]
        public async void UpdateReportedPropertiesWhenCloudOfflineTwinStoredSuccess()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            twin.Properties.Reported = new TwinCollection()
            {
                ["name"] = "oldvalue"
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            string deviceId = "device";
            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value"
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            bool storeMiss = false;
            bool storeHit = false;

            // Act - check if twin is in the cache
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

            // Assert - verify that twin is not in the cache
            Assert.Equal(storeHit, false);
            Assert.Equal(storeMiss, true);

            // Act - update reported properties when twin is not in the cache
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

            // Assert - verify that the twin was fetched
            storeMiss = false;
            storeHit = false;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });
            Assert.Equal(storeHit, true);

            // Arrange - make the cloud offline
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));

            TwinCollection patch = new TwinCollection()
            {
                ["name"] = null,
                ["newname"] = "value"
            };
            TwinCollection merged = new TwinCollection()
            {
                ["newname"] = "value"
            };
            IMessage patchMessage = this.twinCollectionMessageConverter.ToMessage(patch);

            // Act - update local copy of the twin when offline
            await twinManager.UpdateReportedPropertiesAsync(deviceId, patchMessage);

            // Assert - verify that the twin's reported properties was updated and that the patch was stored
            TwinInfo retrieved = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { retrieved = t; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));
            Assert.True(JToken.DeepEquals(
                JsonConvert.DeserializeObject<JToken>(retrieved.Twin.Properties.Reported.ToJson()),
                JsonConvert.DeserializeObject<JToken>(merged.ToJson())));
            Assert.True(JToken.DeepEquals(
                JsonConvert.DeserializeObject<JToken>(retrieved.ReportedPropertiesPatch.ToJson()),
                JsonConvert.DeserializeObject<JToken>(patch.ToJson())));
        }

        [Fact]
        public async void UpdateReportedPropertiesWhenCloudOfflineMalformedPropertiesThrows()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            twin.Properties.Reported = new TwinCollection()
            {
                ["name"] = "oldvalue",
                ["$version"] = 32
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            string deviceId = "device";
            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            bool storeMiss = false;
            bool storeHit = false;

            // Act - check if twin is in the cache
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

            // Assert - verify that twin is not in the cache
            Assert.Equal(storeHit, false);
            Assert.Equal(storeMiss, true);

            // Act - update reported properties when twin is not in the cache
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

            // Assert - verify that the twin was fetched
            storeMiss = false;
            storeHit = false;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });
            Assert.Equal(storeHit, true);

            // Arrange - make the cloud offline
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));

            TwinCollection patch = new TwinCollection()
            {
                ["name"] = null,
                ["malformed"] = 4503599627370496,
            };
            IMessage patchMessage = this.twinCollectionMessageConverter.ToMessage(patch);

            // Act and assert - verify rejection of malformed reported properties
            await Assert.ThrowsAsync<InvalidOperationException>(() => twinManager.UpdateReportedPropertiesAsync(deviceId, patchMessage));
        }

        [Fact]
        public async void UpdateReportedPropertiesWhenCloudOfflineTooLargeCollectionThrows()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            twin.Properties.Reported = new TwinCollection()
            {
                ["name"] = "oldvalue",
                ["$version"] = 32
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            string deviceId = "device";
            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["large"] = new byte[4 * 1024],
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            bool storeMiss = false;
            bool storeHit = false;

            // Act - check if twin is in the cache
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

            // Assert - verify that twin is not in the cache
            Assert.Equal(storeHit, false);
            Assert.Equal(storeMiss, true);

            // Act - update reported properties when twin is not in the cache
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

            // Assert - verify that the twin was fetched
            storeMiss = false;
            storeHit = false;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });
            Assert.Equal(storeHit, true);

            // Arrange - make the cloud offline
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));

            TwinCollection patch = new TwinCollection()
            {
                ["name"] = null,
                ["large"] = new byte[5 * 1024],
            };
            IMessage patchMessage = this.twinCollectionMessageConverter.ToMessage(patch);

            // Act and assert - verify rejection of malformed reported properties
            await Assert.ThrowsAsync<InvalidOperationException>(() => twinManager.UpdateReportedPropertiesAsync(deviceId, patchMessage));
        }

        [Fact]
        public async void AllOperationsWhenCloudOfflineTwinNotStoredThrows()
        {
            // Arrange
            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            string deviceId = "device";

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            // Act and Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage));
            await Assert.ThrowsAsync<InvalidOperationException>(() => twinManager.GetTwinAsync(deviceId));
        }

        [Fact]
        public async void UpdateDesiredPropertiesWhenDeviceProxyOfflineThrows()
        {
            // Arrange
            Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device";

            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            // Act and Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage));
        }

        [Fact]
        public async void GetTwinDoesNotOverwriteSavedReportedPropertiesSuccess()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device";

            bool storeHit = false;
            bool storeMiss = false;

            // Act
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

            // Assert - verify that the twin is not in the store
            Assert.Equal(storeHit, false);
            Assert.Equal(storeMiss, true);

            // Act - get twin
            await twinManager.GetTwinAsync(deviceId);

            // Assert - verify that the twin is in the store
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));
            Assert.Equal(storeHit, true);

            // Arrange - update reported properties when offline
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));

            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "newvalue"
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            // Act - update reported properties in offline mode
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

            // Act - find the twin and reported property patch
            TwinInfo cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t; return Task.CompletedTask; }, () => Task.CompletedTask);

            // Assert - verify that the patch and the twin were updated
            Assert.True(JToken.DeepEquals(
                JsonConvert.DeserializeObject<JToken>(cached.Twin.Properties.Reported.ToJson()),
                JsonConvert.DeserializeObject<JToken>(collection.ToJson())));
            Assert.True(JToken.DeepEquals(
                JsonConvert.DeserializeObject<JToken>(cached.ReportedPropertiesPatch.ToJson()),
                JsonConvert.DeserializeObject<JToken>(collection.ToJson())));

            // Act - get twin so that the local twin gets updated
            await twinManager.GetTwinAsync(deviceId);

            // Assert - verify that the twin was updated but patch was not
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t; return Task.CompletedTask; }, () => Task.CompletedTask);

            Assert.True(JToken.DeepEquals(
                JsonConvert.DeserializeObject<JToken>(cached.ReportedPropertiesPatch.ToJson()),
                JsonConvert.DeserializeObject<JToken>(collection.ToJson())));
        }

        [Fact]
        public async void GetTwinWhenStorePutFailsReturnsLastKnownSuccess()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            Mock<IEntityStore<string, TwinInfo>> mockTwinStore = new Mock<IEntityStore<string, TwinInfo>>();
            mockTwinStore.Setup(t => t.PutOrUpdate(It.IsAny<string>(), It.IsAny<TwinInfo>(), It.IsAny<Func<TwinInfo, TwinInfo>>()))
                .Returns(Task.CompletedTask);

            Option<IEntityStore<string, TwinInfo>> twinStore = Option.Some<IEntityStore<string, TwinInfo>>(mockTwinStore.Object);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, twinStore);

            string deviceId = "device";

            // Act - cache the twin in the store
            IMessage received = await twinManager.GetTwinAsync(deviceId);

            // Assert - verify we got the cloud copy back
            Assert.Equal(received.Body, twinMessage.Body);

            // Arrange - setup failure of twin store
            bool getCalled = false;
            mockTwinStore.Setup(t => t.PutOrUpdate(It.IsAny<string>(), It.IsAny<TwinInfo>(), It.IsAny<Func<TwinInfo, TwinInfo>>()))
                .Throws(new Exception("Out of space"));
            mockTwinStore.Setup(t => t.Get(It.IsAny<string>()))
                .Callback(() => getCalled = true)
                .Returns(Task.FromResult(Option.Some<TwinInfo>(new TwinInfo(twin, null, false))));

            // Arrange - change what the cloud returns
            Twin newTwin = new Twin("d1") { Version = 1 };
            IMessage newTwinMessage = this.twinMessageConverter.ToMessage(twin);
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(newTwinMessage));

            // Act - cache the twin in the store
            received = await twinManager.GetTwinAsync(deviceId);

            // Assert - verify we got the old value of the twin
            Assert.Equal(getCalled, true);
            Assert.Equal(received.Body, twinMessage.Body);
        }

        [Fact]
        public async void UpdateReportedPropertiesWhenStoreThrowsFailure()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            Mock<IEntityStore<string, TwinInfo>> mockTwinStore = new Mock<IEntityStore<string, TwinInfo>>();
            mockTwinStore.Setup(t => t.PutOrUpdate(It.IsAny<string>(), It.IsAny<TwinInfo>(), It.IsAny<Func<TwinInfo, TwinInfo>>()))
                .Returns(Task.CompletedTask);

            Option<IEntityStore<string, TwinInfo>> twinStore = Option.Some<IEntityStore<string, TwinInfo>>(mockTwinStore.Object);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, twinStore);

            mockTwinStore.Setup(t => t.PutOrUpdate(It.IsAny<string>(), It.IsAny<TwinInfo>(), It.IsAny<Func<TwinInfo, TwinInfo>>()))
                .Throws(new Exception("Out of space"));
            mockTwinStore.Setup(t => t.Get(It.IsAny<string>())).ReturnsAsync(Option.None<TwinInfo>());

            string deviceId = "device";
            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "newvalue",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            await Assert.ThrowsAsync<InvalidOperationException>(() => twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage));
        }

        [Fact]
        public async void GetTwinRejectsLowerVersionTwinsSuccess()
        {
            // Arrange - setup twin with version
            Twin twin = new Twin("d1") { Version = 1 };
            twin.Version = 32;
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device";

            // Act - call get twin to cache twin
            IMessage received = await twinManager.GetTwinAsync(deviceId);
            Twin cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t.Twin; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));

            // Assert - verify version of twin matches what we setup
            Assert.Equal(cached.Version, twin.Version);
            Assert.Equal(this.twinMessageConverter.FromMessage(received).Version, twin.Version);

            // Arrange - setup twin with lower than original version
            twin.Version = 30;
            twinMessage = this.twinMessageConverter.ToMessage(twin);
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));

            // Act - call get twin to cache new twin
            received = await twinManager.GetTwinAsync(deviceId);

            cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t.Twin; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));

            // Assert - verify version of twin matches original version
            Assert.Equal(cached.Version, 32);
            Assert.Equal(this.twinMessageConverter.FromMessage(received).Version, 32);
        }

        [Fact]
        public async void GetTwinDoesNotGeneratesDesiredPropertyUpdateIfNotSusbribedSuccess()
        {
            // Arrange - setup twin with version
            Twin twin = new Twin("d1") { Version = 1 };
            twin.Properties.Desired = new TwinCollection { ["$version"] = "32" };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockCloudProxy.Object);

            bool receivedCallback = false;
            Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedCallback = true);
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device";

            // Act - call get twin to cache twin
            IMessage received = await twinManager.GetTwinAsync(deviceId);
            Twin cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t.Twin; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));

            // Assert - verify version of twin matches what we setup
            Assert.Equal(cached.Properties.Desired.Version, twin.Properties.Desired.Version);
            Assert.Equal(this.twinMessageConverter.FromMessage(received).Version, twin.Version);

            // Arrange - setup twin with higher than original version
            twin.Properties.Desired = new TwinCollection { ["$version"] = "33" };
            twinMessage = this.twinMessageConverter.ToMessage(twin);
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));

            // Act - call get twin to cache new twin and to generate the callback
            TwinInfo twinInfo = await twinManager.GetTwinInfoWhenCloudOnlineAsync(deviceId, mockCloudProxy.Object, true);

            cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t.Twin; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));

            // Assert - verify version of twin matches new version and device not subsribed
            Assert.Equal(cached.Properties.Desired.Version, 33);
            Assert.Equal(twinInfo.Twin.Properties.Desired.Version, 33);
            Assert.Equal(twinInfo.SubscribedToDesiredPropertyUpdates, false);

            // Assert - verify desired property update callback was not generated (device was not subscribed to updates)
            Assert.Equal(receivedCallback, false);
        }

        [Fact]
        public async void DesiredPropertyFetchesTwinWithCallbackSuccess()
        {
            // Arrange - make a twin with a version
            Twin twin = new Twin("d1");
            twin.Version = 32;
            twin.Properties.Desired = new TwinCollection()
            {
                ["value"] = "old",
                ["$version"] = 32
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockCloudProxy.Object);

            TwinCollection receivedPatch = new TwinCollection();
            Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedPatch = this.twinCollectionMessageConverter.FromMessage(m))
                .Returns(Task.CompletedTask);
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device";

            // Act - cache a twin
            await twinManager.GetTwinAsync(deviceId);

            Twin cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t.Twin; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));

            // Assert - verify that twin is cached
            Assert.Equal(cached.Properties.Desired.Version, 32);
            Assert.Equal(cached.Version, 32);

            // Arrange - make a patch with original version - 1. setup get twin to return a twin with higher version
            TwinCollection desired = new TwinCollection()
            {
                ["$version"] = 30
            };
            IMessage twinCollectionMessage = this.twinCollectionMessageConverter.ToMessage(desired);
            Twin latest = new Twin();
            twin.Version = 33;
            twin.Properties.Desired = new TwinCollection()
            {
                ["value"] = "latest",
                ["$version"] = 33
            };
            twinMessage = this.twinMessageConverter.ToMessage(twin);
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));

            // Act - update desired properties
            await twinManager.UpdateDesiredPropertiesAsync(deviceId, twinCollectionMessage);

            // Assert - verify that get twin cached the latest twin and called the desired property update callback
            Assert.True(JToken.DeepEquals(
                JsonConvert.DeserializeObject<JToken>(receivedPatch.ToJson()),
                JsonConvert.DeserializeObject<JToken>(twin.Properties.Desired.ToJson())));
        }

        [Fact]
        public async void ConnectionReestablishedReportedPropertiesSyncSuccess()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockCloudProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            TwinCollection reported = new TwinCollection()
            {
                ["value"] = "first"
            };
            IMessage reportedMessage = this.twinCollectionMessageConverter.ToMessage(reported);

            string deviceId = "device";

            bool callbackReceived = false;
            mockCloudProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => callbackReceived = true)
                .Throws(new Exception("not interested"));

            // Act - make twin manager cache reported property patches
            await twinManager.UpdateReportedPropertiesAsync(deviceId, reportedMessage);

            TwinInfo cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));

            // Assert - verify cloud was called and twin manager has collective patch
            Assert.Equal(callbackReceived, true);
            Assert.Equal(cached.ReportedPropertiesPatch.ToJson(), reported.ToJson());

            // Arrange - setup another patch
            reported = new TwinCollection()
            {
                ["value"] = "second"
            };
            reportedMessage = this.twinCollectionMessageConverter.ToMessage(reported);
            callbackReceived = false;

            // Act - make twin manager cache reported properties again
            await twinManager.UpdateReportedPropertiesAsync(deviceId, reportedMessage);

            cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));

            // Assert - verify that twin manager did not attempt to call cloud because there is already
            // a patch
            Assert.Equal(callbackReceived, false);
            Assert.Equal(cached.ReportedPropertiesPatch.ToJson(), reported.ToJson());

            // Arrange - make cloud online
            TwinCollection onlineReported = new TwinCollection();
            callbackReceived = false;
            mockCloudProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>(m =>
                {
                    onlineReported = this.twinCollectionMessageConverter.FromMessage(m);
                    callbackReceived = true;
                })
                .Returns(Task.CompletedTask);

            DeviceIdentity identity = new DeviceIdentity("blah", deviceId, "blah", AuthenticationScope.DeviceKey, "blah", "blah");

            // Act - trigger callback
            twinManager.ConnectionEstablishedCallback(null, identity);

            while (!callbackReceived)
            {
                await Task.Delay(10000); // 10s
            }

            cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));

            // Assert - verify that cloud proxy got the collective patch and that the collective patch is cleared
            Assert.Equal(onlineReported.ToJson(), reported.ToJson());
            Assert.Equal(cached.ReportedPropertiesPatch, new TwinCollection());
        }

        [Fact]
        public async void ConnectionReestablishedGetTwinWithDesiredPropertyUpdateSuccess()
        {
            // Arrange
            Twin twin = new Twin("d1");
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockCloudProxy.Object);

            TwinCollection receivedPatch = new TwinCollection();
            bool callbackReceived = false;
            Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m =>
                {
                    receivedPatch = this.twinCollectionMessageConverter.FromMessage(m);
                    callbackReceived = true;
                })
                .Returns(Task.CompletedTask);
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device";

            TwinCollection desired = new TwinCollection()
            {
                ["$version"] = 30
            };
            IMessage twinCollectionMessage = this.twinCollectionMessageConverter.ToMessage(desired);

            // Act - subscribe to desired property updates
            await twinManager.UpdateDesiredPropertiesAsync(deviceId, twinCollectionMessage);

            TwinInfo cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));

            // Assert - verify the subscribed flag is set
            Assert.Equal(cached.SubscribedToDesiredPropertyUpdates, true);

            // Arrange
            DeviceIdentity identity = new DeviceIdentity("blah", deviceId, "blah", AuthenticationScope.DeviceKey, "blah", "blah");

            twin = new Twin();
            twin.Version = 33;
            twin.Properties.Desired = new TwinCollection()
            {
                ["value"] = "something",
                ["$version"] = 31
            };
            twinMessage = this.twinMessageConverter.ToMessage(twin);

            bool getTwinCalled = false;
            mockCloudProxy.Setup(t => t.GetTwinAsync())
                .Callback(() => getTwinCalled = true)
                .Returns(Task.FromResult(twinMessage));

            // Act - trigger connection callback
            twinManager.ConnectionEstablishedCallback(null, identity);

            while (!callbackReceived)
            {
                await Task.Delay(10000); // 10s
            }

            // Assert - get twin was called and desired property callback was generated
            Assert.Equal(receivedPatch.ToJson(), twin.Properties.Desired.ToJson());
            Assert.Equal(getTwinCalled, true);
        }

        [Fact]
        public async void ConnectionReestablishedDoesNotSyncReportedPropertiesWhenEmptySuccess()
        {
            // Arrange
            Twin twin = new Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockCloudProxy = new Mock<ICloudProxy>();
            bool getTwinReceived = false;
            mockCloudProxy.Setup(t => t.GetTwinAsync())
                .Callback(() => getTwinReceived = true)
                .Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockCloudProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device";

            bool reportedReceived = false;
            mockCloudProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>(m =>
                {
                    reportedReceived = true;
                })
                .Returns(Task.CompletedTask);

            DeviceIdentity identity = new DeviceIdentity("blah", deviceId, "blah", AuthenticationScope.DeviceKey, "blah", "blah");

            // Act - cache the twin so that the twin is in the cache but there are no
            // reported properties cached. Then trigger callback
            await twinManager.GetTwinAsync(deviceId);
            getTwinReceived = false;
            twinManager.ConnectionEstablishedCallback(null, identity);

            while (!getTwinReceived)
            {
                await Task.Delay(2000); // 10s
            }

            // Assert - verify that empty patch didn't trigger reported property callback
            Assert.Equal(reportedReceived, false);
        }

        [Fact]
        public void ValidateTwinPropertiesSuccess()
        {
            string tooLong = Enumerable.Repeat("A", 520).Aggregate((sum, next) => sum + next);
            var reported = new Dictionary<string, string>
            {
                [tooLong] = "wrong"
            };

            Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported)));

            var reported1 = new
            {
                ok = "ok",
                level = new
                {
                    ok = "ok",
                    s = tooLong
                }
            };

            Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported1)));

            var reported2 = new
            {
                level = new
                {
                    number = -4503599627370497
                }
            };

            Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported2)));

            var reported3 = new
            {
                level1 = new
                {
                    level2 = new
                    {
                        level3 = new
                        {
                            level4 = new
                            {
                                level5 = new { }
                            }
                        }
                    }
                }
            };

            TwinManager.ValidateTwinProperties(JToken.FromObject(reported3));

            var reported4 = new
            {
                level1 = new
                {
                    level2 = new
                    {
                        level3 = new
                        {
                            level4 = new
                            {
                                level5 = new
                                {
                                    level6 = new { }
                                }
                            }
                        }
                    }
                }
            };

            Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported4)));

            var reported5 = new
            {
                array = new[] { 0, 1, 2 }
            };

            Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported5)));

            var reported6 = new
            {
                tooBig = new byte[10 * 1024]
            };

            Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported6)));
        }
    }
}
