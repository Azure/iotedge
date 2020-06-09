// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Unit]
    public class TwinManagerTest
    {
        readonly Option<IEntityStore<string, TwinInfo>> twinStore;
        readonly IMessageConverter<TwinCollection> twinCollectionMessageConverter;
        readonly IMessageConverter<Shared.Twin> twinMessageConverter;

        public TwinManagerTest()
        {
            this.twinStore = Option.Some(new StoreProvider(new InMemoryDbStoreProvider()).GetEntityStore<string, TwinInfo>("default"));
            this.twinCollectionMessageConverter = new TwinCollectionMessageConverter();
            this.twinMessageConverter = new TwinMessageConverter();
        }

        public static IEnumerable<object[]> GetTwinKeyData()
        {
            yield return new object[] { "key1", "key1" };

            yield return new object[] { "123", "123" };

            yield return new object[] { "a.b$c d", "a%2Eb%24c%20d" };

            yield return new object[] { "a.b.c.d", "a%2Eb%2Ec%2Ed" };
        }

        [Fact]
        public void TwinManagerConstructorVerifiesArguments()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            Assert.Throws<ArgumentNullException>(() => new TwinManager(null, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore));
            Assert.Throws<ArgumentNullException>(() => new TwinManager(connectionManager, null, this.twinMessageConverter, this.twinStore));
            Assert.Throws<ArgumentNullException>(() => new TwinManager(connectionManager, this.twinCollectionMessageConverter, null, this.twinStore));
        }

        [Fact]
        public void TwinManagerConstructorWithValidArgumentsSucceeds()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            Assert.NotNull(new TwinManager(connectionManager, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore));
            Assert.NotNull(new TwinManager(connectionManager, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>()));
        }

        [Fact]
        public async void GetTwinWhenCloudOnlineTwinNotStoredSuccess()
        {
            // Arrange
            var twin = new Shared.Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device1";

            bool storeHit = false;
            bool storeMiss = false;

            // Act
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });

            // Assert
            Assert.False(storeHit);
            Assert.True(storeMiss);

            // Act
            await twinManager.GetTwinAsync(deviceId);

            // Assert
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));
            Assert.True(storeHit);
        }

        [Fact]
        public async void GetTwinWhenCloudOfflineSuccess()
        {
            // Arrange
            var twin = new Shared.Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device2";
            await twinManager.GetTwinAsync(deviceId);

            bool getTwinCalled = false;
            bool storeHit = false;
            mockProxy.Setup(t => t.GetTwinAsync()).Callback(() => getTwinCalled = true).Throws(new Exception("Offline"));

            // Act
            IMessage received = await twinManager.GetTwinAsync(deviceId);

            // Assert
            Assert.True(getTwinCalled);
            Assert.Equal(received.Body, twinMessage.Body);
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));
            Assert.True(storeHit);
        }

        [Fact]
        public async void GetTwinPassthroughWhenTwinNotStoredSuccess()
        {
            // Arrange
            var twin = new Shared.Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>());

            string deviceId = "device3";

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
            var twin = new Shared.Twin("d1") { Version = 1 };
            twin.Properties.Desired = new TwinCollection()
            {
                ["name"] = "original",
                ["$version"] = 32
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

            bool receivedCallback = false;
            var mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedCallback = true)
                .Returns(Task.CompletedTask);
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device4";
            await twinManager.GetTwinAsync(deviceId);

            var collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            // Act
            await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage);
            TwinInfo patched = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    patched = t;
                    return Task.CompletedTask;
                },
                () => Task.FromResult<TwinInfo>(null));

            // Assert
            Assert.Equal(patched.Twin.Properties.Desired.ToJson(), collection.ToJson());
            Assert.True(receivedCallback);
        }

        [Fact]
        public async void UpdateDesiredPropertiesWhenTwinNotStoredVersionPlus1Success()
        {
            // Arrange
            var twin = new Shared.Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            bool getTwinCalled = false;
            var mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Callback(() => getTwinCalled = true).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

            bool receivedCallback = false;
            var mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedCallback = true)
                .Returns(Task.CompletedTask);
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device5";
            var collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 1
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);
            bool storeHit = false;

            // Act
            await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage);
            TwinInfo updated = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    updated = t;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));

            // Assert
            Assert.True(getTwinCalled);
            Assert.True(storeHit);
            Assert.Equal(updated.Twin.Properties.Desired, collection);
            Assert.True(receivedCallback);
        }

        [Fact]
        public async void UpdateDesiredPropertiesPassthroughSuccess()
        {
            // Arrange
            var received = new TwinCollection();

            var mockDeviceProxy = new Mock<IDeviceProxy>();
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>((m) => { received = this.twinCollectionMessageConverter.FromMessage(m); })
                .Returns(Task.CompletedTask);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            string deviceId = "device6";
            var collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>());

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
            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>((m) => receivedMessage = m)
                .Returns(Task.CompletedTask);
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            string deviceId = "device7";
            var collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>());

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
            var twin = new Shared.Twin("d1") { Version = 1 };
            twin.Properties.Reported = new TwinCollection()
            {
                ["name"] = "oldvalue",
                ["$version"] = 32
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            IMessage receivedMessage = null;
            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>((m) => receivedMessage = m)
                .Returns(Task.CompletedTask);
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            string deviceId = "device8";
            var collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            bool storeMiss = false;
            bool storeHit = false;

            // Act - find the twin
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });

            // Assert - verify that the twin is not in the store
            Assert.False(storeHit);
            Assert.True(storeMiss);

            // Act
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

            // Assert - verify that the message was sent to the cloud proxy
            Assert.Equal(receivedMessage, collectionMessage);

            // Assert - verify that the twin was fetched
            storeMiss = false;
            storeHit = false;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });
            Assert.True(storeHit);

            // Assert - verify that the local twin's reported properties updated.
            // verify that the local patch of reported properties was not updated.
            TwinInfo retrieved = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    retrieved = t;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));
            Assert.False(storeMiss);
            Assert.True(
                JToken.DeepEquals(
                    JsonConvert.DeserializeObject<JToken>(retrieved.Twin.Properties.Reported.ToJson()),
                    JsonConvert.DeserializeObject<JToken>(collection.ToJson())));

            Assert.True(
                JToken.DeepEquals(
                    JsonConvert.DeserializeObject<JToken>(retrieved.ReportedPropertiesPatch.ToJson()),
                    JsonConvert.DeserializeObject<JToken>(new TwinCollection().ToJson())));
        }

        [Fact]
        public async void UpdateReportedPropertiesWhenCloudOfflineTwinNotStoredSuccess()
        {
            // Arrange
            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            string deviceId = "device9";
            var collection1 = new TwinCollection
            {
                ["name"] = "value"
            };
            IMessage collectionMessage1 = this.twinCollectionMessageConverter.ToMessage(collection1);

            var collection2 = new TwinCollection
            {
                ["name2"] = "value2"
            };
            IMessage collectionMessage2 = this.twinCollectionMessageConverter.ToMessage(collection2);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            bool storeMiss = false;
            bool storeHit = false;

            // Act
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });

            // Assert
            Assert.False(storeHit);
            Assert.True(storeMiss);

            // Act
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage1);
            TwinInfo cached1 = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    cached1 = t;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });

            // Assert
            Assert.True(storeHit);
            Assert.Null(cached1.Twin);
            Assert.True(
                JToken.DeepEquals(
                    JsonConvert.DeserializeObject<JToken>(cached1.ReportedPropertiesPatch.ToJson()),
                    JsonConvert.DeserializeObject<JToken>(collection1.ToJson())));

            // Act
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage2);
            TwinInfo cached2 = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    cached2 = t;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });

            // Assert
            Assert.True(storeHit);
            Assert.NotNull(cached2.Twin);
            Assert.True(
                JToken.DeepEquals(
                    JsonConvert.DeserializeObject<JToken>(cached2.ReportedPropertiesPatch.ToJson()),
                    JsonConvert.DeserializeObject<JToken>(collection2.ToJson())));
        }

        [Fact]
        public async void UpdateReportedPropertiesWhenCloudOfflineTwinStoredSuccess()
        {
            // Arrange
            var twin = new Shared.Twin("d1") { Version = 1 };
            twin.Properties.Reported = new TwinCollection()
            {
                ["name"] = "oldvalue"
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            string deviceId = "device10";
            var collection = new TwinCollection()
            {
                ["name"] = "value"
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            bool storeMiss = false;
            bool storeHit = false;

            // Act - check if twin is in the cache
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });

            // Assert - verify that twin is not in the cache
            Assert.False(storeHit);
            Assert.True(storeMiss);

            // Act - update reported properties when twin is not in the cache
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

            // Assert - verify that the twin was fetched
            storeMiss = false;
            storeHit = false;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });
            Assert.True(storeHit);

            // Arrange - make the cloud offline
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));

            var patch = new TwinCollection()
            {
                ["name"] = null,
                ["newname"] = "value"
            };
            var merged = new TwinCollection()
            {
                ["newname"] = "value"
            };
            IMessage patchMessage = this.twinCollectionMessageConverter.ToMessage(patch);

            // Act - update local copy of the twin when offline
            await twinManager.UpdateReportedPropertiesAsync(deviceId, patchMessage);

            // Assert - verify that the twin's reported properties was updated and that the patch was stored
            TwinInfo retrieved = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    retrieved = t;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));
            Assert.True(
                JToken.DeepEquals(
                    JsonConvert.DeserializeObject<JToken>(retrieved.Twin.Properties.Reported.ToJson()),
                    JsonConvert.DeserializeObject<JToken>(merged.ToJson())));
            Assert.True(
                JToken.DeepEquals(
                    JsonConvert.DeserializeObject<JToken>(retrieved.ReportedPropertiesPatch.ToJson()),
                    JsonConvert.DeserializeObject<JToken>(patch.ToJson())));
        }

        [Fact]
        public async void UpdateReportedPropertiesWhenCloudOfflineMalformedPropertiesThrows()
        {
            // Arrange
            var twin = new Shared.Twin("d1") { Version = 1 };
            twin.Properties.Reported = new TwinCollection()
            {
                ["name"] = "oldvalue",
                ["$version"] = 32
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            string deviceId = "device11";
            var collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            bool storeMiss = false;
            bool storeHit = false;

            // Act - check if twin is in the cache
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });

            // Assert - verify that twin is not in the cache
            Assert.False(storeHit);
            Assert.True(storeMiss);

            // Act - update reported properties when twin is not in the cache
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

            // Assert - verify that the twin was fetched
            storeMiss = false;
            storeHit = false;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });
            Assert.True(storeHit);

            // Arrange - make the cloud offline
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));

            var patch = new TwinCollection()
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
            var twin = new Shared.Twin("d1") { Version = 1 };
            twin.Properties.Reported = new TwinCollection()
            {
                ["name"] = "oldvalue",
                ["$version"] = 32
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            string deviceId = "device12";
            var collection = new TwinCollection()
            {
                ["name"] = "value",
                ["large"] = new byte[4 * 1024],
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            bool storeMiss = false;
            bool storeHit = false;

            // Act - check if twin is in the cache
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });

            // Assert - verify that twin is not in the cache
            Assert.False(storeHit);
            Assert.True(storeMiss);

            // Act - update reported properties when twin is not in the cache
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

            // Assert - verify that the twin was fetched
            storeMiss = false;
            storeHit = false;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });
            Assert.True(storeHit);

            // Arrange - make the cloud offline
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));

            var patch = new TwinCollection()
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
            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            var collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            string deviceId = "device13";

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            // Act and Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage));
            await Assert.ThrowsAsync<InvalidOperationException>(() => twinManager.GetTwinAsync(deviceId));
        }

        [Fact]
        public async void UpdateDesiredPropertiesWhenDeviceProxyOfflineThrows()
        {
            // Arrange
            var mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device14";

            var collection = new TwinCollection()
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
            var twin = new Shared.Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device15";

            bool storeHit = false;
            bool storeMiss = false;

            // Act
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () =>
                {
                    storeMiss = true;
                    return Task.FromResult<TwinInfo>(null);
                });

            // Assert - verify that the twin is not in the store
            Assert.False(storeHit);
            Assert.True(storeMiss);

            // Act - get twin
            await twinManager.GetTwinAsync(deviceId);

            // Assert - verify that the twin is in the store
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    storeHit = true;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));
            Assert.True(storeHit);

            // Arrange - update reported properties when offline
            mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Throws(new Exception("Not interested"));

            var collection = new TwinCollection()
            {
                ["name"] = "newvalue"
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            // Act - update reported properties in offline mode
            await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

            // Act - find the twin and reported property patch
            TwinInfo cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    cached = t;
                    return Task.CompletedTask;
                },
                () => Task.CompletedTask);

            // Assert - verify that the patch and the twin were updated
            Assert.True(
                JToken.DeepEquals(
                    JsonConvert.DeserializeObject<JToken>(cached.Twin.Properties.Reported.ToJson()),
                    JsonConvert.DeserializeObject<JToken>(collection.ToJson())));
            Assert.True(
                JToken.DeepEquals(
                    JsonConvert.DeserializeObject<JToken>(cached.ReportedPropertiesPatch.ToJson()),
                    JsonConvert.DeserializeObject<JToken>(collection.ToJson())));

            // Act - get twin so that the local twin gets updated
            await twinManager.GetTwinAsync(deviceId);

            // Assert - verify that the twin was updated but patch was not
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    cached = t;
                    return Task.CompletedTask;
                },
                () => Task.CompletedTask);

            Assert.True(
                JToken.DeepEquals(
                    JsonConvert.DeserializeObject<JToken>(cached.ReportedPropertiesPatch.ToJson()),
                    JsonConvert.DeserializeObject<JToken>(collection.ToJson())));
        }

        [Fact]
        public async void GetTwinWhenStorePutFailsReturnsLastKnownSuccess()
        {
            // Arrange
            var twin = new Shared.Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            var mockTwinStore = new Mock<IEntityStore<string, TwinInfo>>();
            TwinInfo putValue = null;
            mockTwinStore.Setup(t => t.PutOrUpdate(It.IsAny<string>(), It.IsAny<TwinInfo>(), It.IsAny<Func<TwinInfo, TwinInfo>>()))
                .Callback<string, TwinInfo, Func<TwinInfo, TwinInfo>>((s, p, u) => putValue = p)
                .Returns(Task.FromResult(putValue));

            Option<IEntityStore<string, TwinInfo>> twinStoreForTest = Option.Some(mockTwinStore.Object);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, twinStoreForTest);

            string deviceId = "device16";

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
                .Returns(Task.FromResult(Option.Some(new TwinInfo(twin, null))));

            // Arrange - change what the cloud returns
            IMessage newTwinMessage = this.twinMessageConverter.ToMessage(twin);
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(newTwinMessage));

            // Act - cache the twin in the store
            received = await twinManager.GetTwinAsync(deviceId);

            // Assert - verify we got the old value of the twin
            Assert.True(getCalled);
            Assert.Equal(received.Body, twinMessage.Body);
        }

        [Fact]
        public async void UpdateReportedPropertiesWhenStoreThrowsFailure()
        {
            // Arrange
            var twin = new Shared.Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            var mockTwinStore = new Mock<IEntityStore<string, TwinInfo>>();
            mockTwinStore.Setup(t => t.Get(It.IsAny<string>())).ReturnsAsync(Option.Some(new TwinInfo(twin, new TwinCollection())));
            mockTwinStore.Setup(t => t.Update(It.IsAny<string>(), It.IsAny<Func<TwinInfo, TwinInfo>>()))
                .Throws(new InvalidOperationException("Out of space"));

            Option<IEntityStore<string, TwinInfo>> twinStoreValue = Option.Some(mockTwinStore.Object);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, twinStoreValue);

            string deviceId = "device17";
            var collection = new TwinCollection()
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
            var twin = new Shared.Twin("d1") { Version = 1 };
            twin.Version = 32;
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device18";

            // Act - call get twin to cache twin
            IMessage received = await twinManager.GetTwinAsync(deviceId);
            Shared.Twin cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    cached = t.Twin;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));

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
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    cached = t.Twin;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));

            // Assert - verify version of twin matches original version
            Assert.Equal(32, cached.Version);
            Assert.Equal(32, this.twinMessageConverter.FromMessage(received).Version);
        }

        [Fact]
        public async void GetTwinDoesNotGeneratesDesiredPropertyUpdateIfNotSusbribedSuccess()
        {
            // Arrange - setup twin with version
            var twin = new Shared.Twin("d1") { Version = 1 };
            twin.Properties.Desired = new TwinCollection { ["$version"] = "32" };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

            bool receivedCallback = false;
            var mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedCallback = true);
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device19";

            // Act - call get twin to cache twin
            IMessage received = await twinManager.GetTwinAsync(deviceId);
            Shared.Twin cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    cached = t.Twin;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));

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
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    cached = t.Twin;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));

            // Assert - verify version of twin matches new version and device not subsribed
            Assert.Equal(33, cached.Properties.Desired.Version);
            Assert.Equal(33, twinInfo.Twin.Properties.Desired.Version);

            // Assert - verify desired property update callback was not generated (device was not subscribed to updates)
            Assert.False(receivedCallback);
        }

        [Fact]
        public async void DesiredPropertyFetchesTwinWithCallbackSuccess()
        {
            // Arrange - make a twin with a version
            string deviceId = "device20";
            var twin = new Shared.Twin(deviceId);
            twin.Version = 32;
            twin.Properties.Desired = new TwinCollection
            {
                ["value"] = "old",
                ["$version"] = 32
            };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

            var receivedPatch = new TwinCollection();
            var mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedPatch = this.twinCollectionMessageConverter.FromMessage(m))
                .Returns(Task.CompletedTask);
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            IReadOnlyDictionary<DeviceSubscription, bool> deviceSubscriptions = new ReadOnlyDictionary<DeviceSubscription, bool>(
                new Dictionary<DeviceSubscription, bool>
                {
                    [DeviceSubscription.DesiredPropertyUpdates] = true
                });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);
            connectionManager.Setup(t => t.GetSubscriptions(deviceId))
                .Returns(Option.Some(deviceSubscriptions));

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            // Act - cache a twin
            await twinManager.GetTwinAsync(deviceId);

            Shared.Twin cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    cached = t.Twin;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));

            // Assert - verify that twin is cached
            Assert.Equal(32, cached.Properties.Desired.Version);
            Assert.Equal(32, cached.Version);

            // Arrange - make a patch with original version - 1. setup get twin to return a twin with higher version
            var desired = new TwinCollection()
            {
                ["$version"] = 30
            };
            IMessage twinCollectionMessage = this.twinCollectionMessageConverter.ToMessage(desired);
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
            Assert.True(
                JToken.DeepEquals(
                    JsonConvert.DeserializeObject<JToken>(receivedPatch.ToJson()),
                    JsonConvert.DeserializeObject<JToken>(twin.Properties.Desired.ToJson())));
        }

        [Fact]
        public async void ConnectionReestablishedReportedPropertiesSyncSuccess()
        {
            // Arrange
            var twin = new Shared.Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            var reported = new TwinCollection()
            {
                ["value"] = "first"
            };
            IMessage reportedMessage = this.twinCollectionMessageConverter.ToMessage(reported);

            string deviceId = "device21";

            bool callbackReceived = false;
            mockCloudProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => callbackReceived = true)
                .Throws(new Exception("not interested"));

            // Act - make twin manager cache reported property patches
            await twinManager.UpdateReportedPropertiesAsync(deviceId, reportedMessage);

            TwinInfo cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    cached = t;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));

            // Assert - verify cloud was called and twin manager has collective patch
            Assert.True(callbackReceived);
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
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    cached = t;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));

            // Assert - verify that twin manager did not attempt to call cloud because there is already
            // a patch
            Assert.False(callbackReceived);
            Assert.Equal(cached.ReportedPropertiesPatch.ToJson(), reported.ToJson());

            // Arrange - make cloud online
            var onlineReported = new TwinCollection();
            callbackReceived = false;
            mockCloudProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>(
                    m =>
                    {
                        onlineReported = this.twinCollectionMessageConverter.FromMessage(m);
                        callbackReceived = true;
                    })
                .Returns(Task.CompletedTask);

            var identity = Mock.Of<IIdentity>(i => i.Id == deviceId);

            // Act - trigger callback
            twinManager.ConnectionEstablishedCallback(null, identity);

            while (!callbackReceived)
            {
                await Task.Delay(10000); // 10s
            }

            cached = null;
            await twinManager.ExecuteOnTwinStoreResultAsync(
                deviceId,
                t =>
                {
                    cached = t;
                    return Task.FromResult(t);
                },
                () => Task.FromResult<TwinInfo>(null));

            // Assert - verify that cloud proxy got the collective patch and that the collective patch is cleared
            Assert.Equal(onlineReported.ToJson(), reported.ToJson());
            Assert.Equal(cached.ReportedPropertiesPatch, new TwinCollection());
        }

        [Fact]
        public async void ConnectionReestablishedGetTwinWithDesiredPropertyUpdateSuccess()
        {
            // Arrange
            string deviceId = "device22";
            var twin = new Shared.Twin(deviceId);
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

            var receivedPatch = new TwinCollection();
            bool callbackReceived = false;
            var mockDeviceProxy = new Mock<IDeviceProxy>();
            mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(
                    m =>
                    {
                        receivedPatch = this.twinCollectionMessageConverter.FromMessage(m);
                        callbackReceived = true;
                    })
                .Returns(Task.CompletedTask);
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            IReadOnlyDictionary<DeviceSubscription, bool> deviceSubscriptions = new ReadOnlyDictionary<DeviceSubscription, bool>(
                new Dictionary<DeviceSubscription, bool>
                {
                    [DeviceSubscription.DesiredPropertyUpdates] = true
                });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);
            connectionManager.Setup(t => t.GetSubscriptions(deviceId))
                .Returns(Option.Some(deviceSubscriptions));

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            var desired = new TwinCollection()
            {
                ["$version"] = 30
            };
            IMessage twinCollectionMessage = this.twinCollectionMessageConverter.ToMessage(desired);

            // Act - subscribe to desired property updates
            await twinManager.UpdateDesiredPropertiesAsync(deviceId, twinCollectionMessage);

            await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => Task.FromResult(t), () => Task.FromResult<TwinInfo>(null));

            // Arrange
            var identity = Mock.Of<IIdentity>(i => i.Id == deviceId);

            twin = new Shared.Twin();
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
            Assert.True(getTwinCalled);
        }

        [Fact]
        public async void ConnectionReestablishedDoesNotSyncReportedPropertiesWhenEmptySuccess()
        {
            // Arrange
            var twin = new Shared.Twin("d1") { Version = 1 };
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            var mockCloudProxy = new Mock<ICloudProxy>();
            bool getTwinReceived = false;
            mockCloudProxy.Setup(t => t.GetTwinAsync())
                .Callback(() => getTwinReceived = true)
                .Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));

            var twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = "device23";

            bool reportedReceived = false;
            mockCloudProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => { reportedReceived = true; })
                .Returns(Task.CompletedTask);

            var identity = Mock.Of<IIdentity>(i => i.Id == "blah");

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
            Assert.False(reportedReceived);
        }

        [Fact]
        public void ValidateTwinPropertiesWithLongName()
        {
            string tooLong = Enumerable.Repeat("A", 5000).Aggregate((sum, next) => sum + next);
            var reported = new Dictionary<string, string>
            {
                [tooLong] = "wrong"
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported)));
            Assert.Equal("Length of property name AAAAAAAAAA.. exceeds maximum length of 4096", ex.Message);
        }

        [Fact]
        public void ValidateTwinPropertiesWithLongValue()
        {
            string tooLong = Enumerable.Repeat("A", 5000).Aggregate((sum, next) => sum + next);
            var reported = new
            {
                ok = "ok",
                level1 = new
                {
                    ok = "ok",
                    level2 = new
                    {
                        propertyWithBigValue = tooLong
                    }
                }
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported)));
            Assert.Equal("Value associated with property name propertyWithBigValue has length 5000 that exceeds maximum length of 4096", ex.Message);
        }

        [Fact]
        public void ValidateTwinPropertiesWithNullValue()
        {
            var reportedObj = new
            {
                ok = "ok",
                level1 = new
                {
                    ok = null as string
                }
            };

            string reportedJson = "{ \"ok\":\"good\", \"level1\": { \"field1\": null } }";

            TwinManager.ValidateTwinProperties(JToken.FromObject(reportedObj));
            TwinManager.ValidateTwinProperties(JToken.Parse(reportedJson));
        }

        [Fact]
        public void ValidateTwinPropertiesWithInvalidNumber()
        {
            var reported = new
            {
                level = new
                {
                    invalidNumber = -4503599627370497
                }
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported)));
            Assert.Equal("Property invalidNumber has an out of bound value. Valid values are between -4503599627370496 and 4503599627370495", ex.Message);
        }

        [Fact]
        public void ValidateTwinPropertiesWithTooManyLevel()
        {
            var reported = new
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

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported)));
            Assert.Equal("Nested depth of twin property exceeds 5", ex.Message);
        }

        [Fact]
        public void ValidateTwinPropertiesWithArray()
        {
            var reported = new
            {
                array = new[] { 0, 1, 2 }
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported)));
            Assert.Equal("Property array has a value of unsupported type. Valid types are integer, float, string, bool, null and nested object", ex.Message);
        }

        [Fact]
        public void ValidateTwinPropertiesWithBtyeValue()
        {
            var reported = new
            {
                tooBig = new byte[10 * 1024]
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TwinManager.ValidateTwinProperties(JToken.FromObject(reported)));
            Assert.Equal("Property tooBig has a value of unsupported type. Valid types are integer, float, string, bool, null and nested object", ex.Message);
        }

        [Fact]
        public void ValidateTwinPropertiesSuccess()
        {
            var reported = new
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

            TwinManager.ValidateTwinProperties(JToken.FromObject(reported));
        }

        [Theory]
        [MemberData(nameof(GetTwinKeyData))]
        public void EncodeTwinKeyTest(string input, string expectedResult)
        {
            string result = TwinManager.EncodeTwinKey(input);
            Assert.Equal(expectedResult, result);
        }
    }
}
