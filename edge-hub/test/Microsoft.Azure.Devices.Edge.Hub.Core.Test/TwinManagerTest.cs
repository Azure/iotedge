namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Moq;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    [Unit]
    public class TwinManagerTest
    {
        Option<IEntityStore<string, Twin>> twinStore;
        IMessageConverter<TwinCollection> twinCollectionMessageConverter;
        IMessageConverter<Twin> twinMessageConverter;

        public TwinManagerTest()
        {
            this.twinStore = Option.Some(new StoreProvider(new InMemoryDbStoreProvider()).GetEntityStore<string, Twin>("default"));
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
            Assert.NotNull(new TwinManager(connectionManager, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, Twin>>()));
        }

        [Fact]
        public async void GetTwinFromCloudWhenNotStoredStoresTwin()
        {
            // Arrange
            Twin twin = new Twin();
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = String.Format("%s", 1);

            bool storeHit = false;
            bool storeMiss = false;

            // Act
            await twinManager.GetTwinFromStoreAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<Twin>(null); });

            // Assert
            Assert.Equal(storeHit, false);
            Assert.Equal(storeMiss, true);

            // Act
            IMessage received = await twinManager.GetTwinAsync(deviceId);

            // Assert
            await twinManager.GetTwinFromStoreAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => Task.FromResult<Twin>(null));
            Assert.Equal(storeHit, true);
        }

        [Fact]
        public async void GetTwinWhenCloudThrowsRetrievesTwin()
        {
            // Arrange
            Twin twin = new Twin();
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = String.Format("%s", 2);
            await twinManager.GetTwinAsync(deviceId);

            bool getTwinCalled = false;
            bool storeHit = false;
            mockProxy.Setup(t => t.GetTwinAsync()).Callback(() => getTwinCalled = true).Throws(new Exception("Offline"));

            // Act
            IMessage received = await twinManager.GetTwinAsync(deviceId);

            // Assert
            Assert.Equal(getTwinCalled, true);
            Assert.Equal(received.Body, twinMessage.Body);
            await twinManager.GetTwinFromStoreAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => Task.FromResult<Twin>(null));
            Assert.Equal(storeHit, true);
        }

        [Fact]
        public async void GetTwinPassthroughReturnsNotStoredTwin()
        {
            // Arrange
            Twin twin = new Twin();
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
            mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, Twin>>());

            string deviceId = String.Format("%s", 3);

            // Act
            IMessage received = await twinManager.GetTwinAsync(deviceId);

            // Assert
            Assert.Equal(received.Body, twinMessage.Body);
            Assert.Equal(twinManager.twinStore, Option.None<IEntityStore<string, Twin>>());
        }

        public async void UpdateDesiredPropertiesWithStoredTwinSuccess()
        {
            // Arrange
            Twin twin = new Twin();
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            Mock<ICloudProxy> mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

            Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = String.Format("%s", 4);
            IMessage received = await twinManager.GetTwinAsync(deviceId);

            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            // Act
            await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage);
            Twin patched = await twinManager.GetTwinFromStoreAsync(deviceId, t => Task.FromResult(t), () => Task.FromResult<Twin>(null));

            // Assert
            Assert.Equal(patched.Properties.Desired, collection);
        }

        [Fact]
        public async void UpdateDesiredPropertiesWhenTwinNotStoredSuccess()
        {
            // Arrange
            Twin twin = new Twin();
            IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

            bool getTwinCalled = false;
            Mock<ICloudProxy> mockCloudProxy = new Mock<ICloudProxy>();
            mockCloudProxy.Setup(t => t.GetTwinAsync()).Callback(() => getTwinCalled = true).Returns(Task.FromResult(twinMessage));
            Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

            Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
            Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

            Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);
            connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

            string deviceId = String.Format("%s", 5);
            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);
            bool storeHit = false;

            // Act
            await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage);
            await twinManager.GetTwinFromStoreAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => Task.FromResult<Twin>(null));

            // Assert
            Assert.Equal(getTwinCalled, true);
            Assert.Equal(storeHit, true);
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

            string deviceId = String.Format("%s", 6);
            TwinCollection collection = new TwinCollection()
            {
                ["name"] = "value",
                ["$version"] = 33
            };
            IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

            TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, Twin>>());

            // Act
            await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage);

            // Assert
            Assert.Equal(received, collection);
            Assert.Equal(twinManager.twinStore, Option.None<IEntityStore<string, Twin>>());
        }
    }
}
