// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Twin
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Twin;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Xunit;

    [Unit]
    public class StoringTwinManagerTest
    {
        [Fact]
        public async Task GetTwinTest()
        {
            // Arrange
            string id = "d1";

            var desired1 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            var reported1 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            var twin1 = new TwinProperties
            {
                Desired = desired1,
                Reported = reported1
            };

            var cloudSync = new Mock<ICloudSync>();
            cloudSync.SetupSequence(c => c.GetTwin(id))
                .ReturnsAsync(Option.None<TwinProperties>())
                .ReturnsAsync(Option.Some(twin1))
                .ReturnsAsync(Option.None<TwinProperties>());

            TwinProperties receivedTwin = null;
            var twinStore = new Mock<ITwinStore>();
            twinStore.Setup(c => c.Update(id, It.IsAny<TwinProperties>()))
                .Callback<string, TwinProperties>((s, t) => receivedTwin = t)
                .Returns(Task.CompletedTask);

            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(() => Option.Maybe(receivedTwin));

            var twinMessageConverter = new TwinMessageConverter();
            var connectionManager = Mock.Of<IConnectionManager>();
            var twinCollectionConverter = Mock.Of<IMessageConverter<PropertyCollection>>();
            var reportedPropertiesValidator = Mock.Of<IValidator<PropertyCollection>>();
            var reportedPropertiesStore = Mock.Of<IReportedPropertiesStore>();
            var deviceConnectivityManager = Mock.Of<IDeviceConnectivityManager>();

            var twinManager = new StoringTwinManager(
                connectionManager,
                twinCollectionConverter,
                twinMessageConverter,
                reportedPropertiesValidator,
                twinStore.Object,
                reportedPropertiesStore,
                cloudSync.Object,
                deviceConnectivityManager,
                TimeSpan.FromMinutes(10));

            // Act
            IMessage twinMessage = await twinManager.GetTwinAsync(id);

            // Assert
            Assert.NotNull(twinMessage);
            TwinProperties twin = twinMessageConverter.FromMessage(twinMessage);
            Assert.NotNull(twin);
            Assert.Equal("{}", twin.Desired.GetSerializedString());
            Assert.Equal("{}", twin.Reported.GetSerializedString());

            // Act
            twinMessage = await twinManager.GetTwinAsync(id);

            // Assert
            Assert.NotNull(twinMessage);
            twin = twinMessageConverter.FromMessage(twinMessage);
            Assert.NotNull(twin);
            Assert.NotNull(receivedTwin);
            Assert.Equal(receivedTwin.Desired.GetSerializedString(), twin.Desired.GetSerializedString());
            Assert.Equal(receivedTwin.Reported.GetSerializedString(), twin.Reported.GetSerializedString());
            Assert.Equal(twin1.Desired.GetSerializedString(), twin.Desired.GetSerializedString());
            Assert.Equal(twin1.Reported.GetSerializedString(), twin.Reported.GetSerializedString());

            // Act
            twinMessage = await twinManager.GetTwinAsync(id);

            // Assert
            Assert.NotNull(twinMessage);
            twin = twinMessageConverter.FromMessage(twinMessage);
            Assert.NotNull(twin);
            Assert.NotNull(receivedTwin);
            Assert.Equal(receivedTwin.Desired.GetSerializedString(), twin.Desired.GetSerializedString());
            Assert.Equal(receivedTwin.Reported.GetSerializedString(), twin.Reported.GetSerializedString());
            Assert.Equal(twin1.Desired.GetSerializedString(), twin.Desired.GetSerializedString());
            Assert.Equal(twin1.Reported.GetSerializedString(), twin.Reported.GetSerializedString());
        }

        [Fact]
        public async Task UpdateReportedPropertiesTest()
        {
            string id = "d1";

            var reported1 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3"
            };

            PropertyCollection receivedTwinPatch = null;
            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.UpdateReportedProperties(id, It.IsAny<PropertyCollection>()))
                .Callback<string, PropertyCollection>((s, t) => receivedTwinPatch = t)
                .Returns(Task.CompletedTask);

            PropertyCollection receivedTwinPatch2 = null;
            var reportedPropertiesStore = new Mock<IReportedPropertiesStore>(MockBehavior.Strict);
            reportedPropertiesStore.Setup(r => r.InitSyncToCloud(id));
            reportedPropertiesStore.Setup(r => r.Update(id, It.IsAny<PropertyCollection>()))
                .Callback<string, PropertyCollection>((s, t) => receivedTwinPatch2 = t)
                .Returns(Task.CompletedTask);

            var cloudSync = Mock.Of<ICloudSync>();
            var twinMessageConverter = new TwinMessageConverter();
            var connectionManager = Mock.Of<IConnectionManager>();
            var twinCollectionConverter = new PropertyCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<PropertyCollection>>();
            var deviceConnectivityManager = Mock.Of<IDeviceConnectivityManager>();

            var twinManager = new StoringTwinManager(
                connectionManager,
                twinCollectionConverter,
                twinMessageConverter,
                reportedPropertiesValidator,
                twinStore.Object,
                reportedPropertiesStore.Object,
                cloudSync,
                deviceConnectivityManager,
                TimeSpan.FromMinutes(10));

            IMessage reportedPropertiesMessage = twinCollectionConverter.ToMessage(reported1);

            // Act
            await twinManager.UpdateReportedPropertiesAsync(id, reportedPropertiesMessage);

            // Assert
            twinStore.VerifyAll();
            reportedPropertiesStore.VerifyAll();

            Assert.NotNull(receivedTwinPatch);
            Assert.NotNull(receivedTwinPatch2);
            Assert.Equal(reported1.GetSerializedString(), receivedTwinPatch.GetSerializedString());
            Assert.Equal(reported1.GetSerializedString(), receivedTwinPatch2.GetSerializedString());
        }

        [Fact]
        public async Task UpdateDesiredPropertiesTest()
        {
            string id = "d1";

            var desired0 = new PropertyCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var reported0 = new PropertyCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var twinBase = new TwinProperties
            {
                Reported = reported0,
                Desired = desired0
            };

            var desired1 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            PropertyCollection receivedTwinPatch = null;
            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.UpdateDesiredProperties(id, It.IsAny<PropertyCollection>()))
                .Callback<string, PropertyCollection>((s, t) => receivedTwinPatch = t)
                .Returns(Task.CompletedTask);

            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(Option.Some(twinBase));

            var reportedPropertiesStore = new Mock<IReportedPropertiesStore>(MockBehavior.Strict);

            IMessage receivedTwinPatchMessage = null;
            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedTwinPatchMessage = m)
                .Returns(Task.CompletedTask);

            var cloudSync = Mock.Of<ICloudSync>();
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionConverter = new PropertyCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<PropertyCollection>>();
            var deviceConnectivityManager = Mock.Of<IDeviceConnectivityManager>();

            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.CheckClientSubscription(id, DeviceSubscription.DesiredPropertyUpdates)
                    && c.GetDeviceConnection(id) == Option.Some(deviceProxy.Object));

            var twinManager = new StoringTwinManager(
                connectionManager,
                twinCollectionConverter,
                twinMessageConverter,
                reportedPropertiesValidator,
                twinStore.Object,
                reportedPropertiesStore.Object,
                cloudSync,
                deviceConnectivityManager,
                TimeSpan.FromMinutes(10));

            IMessage desiredPropertiesMessage = twinCollectionConverter.ToMessage(desired1);

            // Act
            await twinManager.UpdateDesiredPropertiesAsync(id, desiredPropertiesMessage);

            // Assert
            twinStore.VerifyAll();
            reportedPropertiesStore.VerifyAll();

            Assert.NotNull(receivedTwinPatch);
            Assert.NotNull(receivedTwinPatchMessage);
            Assert.Equal(desired1.GetSerializedString(), receivedTwinPatch.GetSerializedString());
            PropertyCollection receivedTwinPatch2 = twinCollectionConverter.FromMessage(receivedTwinPatchMessage);
            Assert.Equal(desired1.GetSerializedString(), receivedTwinPatch2.GetSerializedString());
        }

        [Fact]
        public async Task UpdateDesiredPropertiesWithIncorrectPatchTest()
        {
            string id = "d1";

            var desired0 = new PropertyCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var reported0 = new PropertyCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var twinBase = new TwinProperties
            {
                Reported = reported0,
                Desired = desired0
            };

            var desired2 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p2"] = "v2",
                ["p3"] = "v3",
                ["$version"] = 2
            };

            var reported2 = new PropertyCollection
            {
                ["p2"] = "vp2",
                ["$version"] = 2
            };

            var twin2 = new TwinProperties
            {
                Reported = reported2,
                Desired = desired2
            };

            var desired2Patch = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["$version"] = 2
            };

            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(Option.Some(twinBase));

            TwinProperties storedTwin = null;
            twinStore.Setup(c => c.Update(id, It.IsAny<TwinProperties>()))
                .Callback<string, TwinProperties>((s, t) => storedTwin = t)
                .Returns(Task.CompletedTask);

            var reportedPropertiesStore = new Mock<IReportedPropertiesStore>(MockBehavior.Strict);

            IMessage receivedTwinPatchMessage = null;
            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedTwinPatchMessage = m)
                .Returns(Task.CompletedTask);

            var cloudSync = Mock.Of<ICloudSync>(c => c.GetTwin(id) == Task.FromResult(Option.Some(twin2)));
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionConverter = new PropertyCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<PropertyCollection>>();
            var deviceConnectivityManager = Mock.Of<IDeviceConnectivityManager>();

            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.CheckClientSubscription(id, DeviceSubscription.DesiredPropertyUpdates)
                    && c.GetDeviceConnection(id) == Option.Some(deviceProxy.Object));

            var twinManager = new StoringTwinManager(
                connectionManager,
                twinCollectionConverter,
                twinMessageConverter,
                reportedPropertiesValidator,
                twinStore.Object,
                reportedPropertiesStore.Object,
                cloudSync,
                deviceConnectivityManager,
                TimeSpan.FromMinutes(10));

            IMessage desiredPropertiesMessage = twinCollectionConverter.ToMessage(desired2Patch);

            // Act
            await twinManager.UpdateDesiredPropertiesAsync(id, desiredPropertiesMessage);

            // Assert
            twinStore.VerifyAll();
            reportedPropertiesStore.VerifyAll();

            Assert.NotNull(storedTwin);
            Assert.NotNull(receivedTwinPatchMessage);
            Assert.Equal(twin2.Desired.GetSerializedString(), storedTwin.Desired.GetSerializedString());
            Assert.Equal(twin2.Reported.GetSerializedString(), storedTwin.Reported.GetSerializedString());
            PropertyCollection receivedTwinPatch2 = twinCollectionConverter.FromMessage(receivedTwinPatchMessage);
            Assert.Equal("{\"p0\":null,\"$version\":2,\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":\"v3\"}", receivedTwinPatch2.GetSerializedString());
        }

        [Fact]
        public async Task DeviceConnectionTest()
        {
            string id = "d1";
            var identity = Mock.Of<IIdentity>(i => i.Id == id);

            var desired0 = new PropertyCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var reported0 = new PropertyCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var twinBase = new TwinProperties
            {
                Reported = reported0,
                Desired = desired0
            };

            var desired2 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p2"] = "v2",
                ["p3"] = "v3",
                ["$version"] = 2
            };

            var reported2 = new PropertyCollection
            {
                ["p2"] = "vp2",
                ["$version"] = 2
            };

            var twin2 = new TwinProperties
            {
                Reported = reported2,
                Desired = desired2
            };

            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(Option.Some(twinBase));

            TwinProperties storedTwin = null;
            twinStore.Setup(c => c.Update(id, It.IsAny<TwinProperties>()))
                .Callback<string, TwinProperties>((s, t) => storedTwin = t)
                .Returns(Task.CompletedTask);

            var reportedPropertiesStore = new Mock<IReportedPropertiesStore>(MockBehavior.Strict);
            reportedPropertiesStore.Setup(r => r.SyncToCloud(id))
                .Returns(Task.CompletedTask);

            IMessage receivedTwinPatchMessage = null;
            var deviceProxy = new Mock<IDeviceProxy>(MockBehavior.Strict);
            deviceProxy.Setup(d => d.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedTwinPatchMessage = m)
                .Returns(Task.CompletedTask);

            var cloudSync = new Mock<ICloudSync>(MockBehavior.Strict);
            cloudSync.Setup(c => c.GetTwin(id))
                .ReturnsAsync(Option.Some(twin2));
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionConverter = new PropertyCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<PropertyCollection>>();

            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.CheckClientSubscription(id, DeviceSubscription.DesiredPropertyUpdates)
                    && c.GetDeviceConnection(id) == Option.Some(deviceProxy.Object)
                    && c.GetConnectedClients() == new[] { identity });

            var deviceConnectivityManager = new Mock<IDeviceConnectivityManager>();

            var twinManager = new StoringTwinManager(
                connectionManager,
                twinCollectionConverter,
                twinMessageConverter,
                reportedPropertiesValidator,
                twinStore.Object,
                reportedPropertiesStore.Object,
                cloudSync.Object,
                deviceConnectivityManager.Object,
                TimeSpan.FromMinutes(10));

            // Act
            deviceConnectivityManager.Raise(d => d.DeviceConnected += null, this, new EventArgs());

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(3));
            twinStore.VerifyAll();
            reportedPropertiesStore.VerifyAll();
            cloudSync.VerifyAll();
            deviceProxy.VerifyAll();
            Mock.Get(connectionManager).VerifyAll();

            Assert.NotNull(storedTwin);
            Assert.NotNull(receivedTwinPatchMessage);
            Assert.Equal(twin2.Desired.GetSerializedString(), storedTwin.Desired.GetSerializedString());
            Assert.Equal(twin2.Reported.GetSerializedString(), storedTwin.Reported.GetSerializedString());
            PropertyCollection receivedTwinPatch2 = twinCollectionConverter.FromMessage(receivedTwinPatchMessage);
            Assert.Equal("{\"p0\":null,\"$version\":2,\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":\"v3\"}", receivedTwinPatch2.GetSerializedString());
        }

        [Fact]
        public async Task DeviceConnectionWithEmptyPatchTest()
        {
            string id = "d1";
            var identity = Mock.Of<IIdentity>(i => i.Id == id);

            var desired0 = new PropertyCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var reported0 = new PropertyCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var twinBase = new TwinProperties
            {
                Reported = reported0,
                Desired = desired0
            };

            var desired2 = new PropertyCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var reported2 = new PropertyCollection
            {
                ["p2"] = "vp2",
                ["$version"] = 2
            };

            var twin2 = new TwinProperties
            {
                Reported = reported2,
                Desired = desired2
            };

            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(Option.Some(twinBase));

            TwinProperties storedTwin = null;
            twinStore.Setup(c => c.Update(id, It.IsAny<TwinProperties>()))
                .Callback<string, TwinProperties>((s, t) => storedTwin = t)
                .Returns(Task.CompletedTask);

            var reportedPropertiesStore = new Mock<IReportedPropertiesStore>(MockBehavior.Strict);
            reportedPropertiesStore.Setup(r => r.SyncToCloud(id))
                .Returns(Task.CompletedTask);

            IMessage receivedTwinPatchMessage = null;
            var deviceProxy = new Mock<IDeviceProxy>(MockBehavior.Strict);
            deviceProxy.Setup(d => d.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedTwinPatchMessage = m)
                .Returns(Task.CompletedTask);

            var cloudSync = new Mock<ICloudSync>(MockBehavior.Strict);
            cloudSync.Setup(c => c.GetTwin(id))
                .ReturnsAsync(Option.Some(twin2));
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionConverter = new PropertyCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<PropertyCollection>>();

            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.CheckClientSubscription(id, DeviceSubscription.DesiredPropertyUpdates)
                    && c.GetDeviceConnection(id) == Option.Some(deviceProxy.Object)
                    && c.GetConnectedClients() == new[] { identity });

            var deviceConnectivityManager = new Mock<IDeviceConnectivityManager>();

            var twinManager = new StoringTwinManager(
                connectionManager,
                twinCollectionConverter,
                twinMessageConverter,
                reportedPropertiesValidator,
                twinStore.Object,
                reportedPropertiesStore.Object,
                cloudSync.Object,
                deviceConnectivityManager.Object,
                TimeSpan.FromMinutes(10));

            // Act
            deviceConnectivityManager.Raise(d => d.DeviceConnected += null, this, new EventArgs());

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            twinStore.VerifyAll();
            reportedPropertiesStore.VerifyAll();
            cloudSync.VerifyAll();
            deviceProxy.Verify(d => d.OnDesiredPropertyUpdates(It.IsAny<IMessage>()), Times.Never);

            Assert.NotNull(storedTwin);
            Assert.Null(receivedTwinPatchMessage);
            Assert.Equal(twin2.Desired.GetSerializedString(), storedTwin.Desired.GetSerializedString());
            Assert.Equal(twin2.Reported.GetSerializedString(), storedTwin.Reported.GetSerializedString());
        }

        [Fact]
        public async Task DeviceConnectionNoSubscriptionTest()
        {
            string id = "d1";
            var identity = Mock.Of<IIdentity>(i => i.Id == id);

            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(Option.None<TwinProperties>());

            var reportedPropertiesStore = new Mock<IReportedPropertiesStore>(MockBehavior.Strict);
            reportedPropertiesStore.Setup(r => r.SyncToCloud(id))
                .Returns(Task.CompletedTask);

            var deviceProxy = new Mock<IDeviceProxy>(MockBehavior.Strict);
            var cloudSync = new Mock<ICloudSync>(MockBehavior.Strict);
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionConverter = new PropertyCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<PropertyCollection>>();

            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.CheckClientSubscription(id, DeviceSubscription.DesiredPropertyUpdates) == false
                    && c.GetConnectedClients() == new[] { identity });

            var deviceConnectivityManager = new Mock<IDeviceConnectivityManager>();

            var twinManager = new StoringTwinManager(
                connectionManager,
                twinCollectionConverter,
                twinMessageConverter,
                reportedPropertiesValidator,
                twinStore.Object,
                reportedPropertiesStore.Object,
                cloudSync.Object,
                deviceConnectivityManager.Object,
                TimeSpan.FromMinutes(10));

            // Act
            deviceConnectivityManager.Raise(d => d.DeviceConnected += null, this, new EventArgs());

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(3));
            twinStore.VerifyAll();
            reportedPropertiesStore.VerifyAll();
            Mock.Get(connectionManager).VerifyAll();
            cloudSync.VerifyAll();
            deviceProxy.VerifyAll();
        }

        [Fact]
        public async Task DeviceConnectionSyncPeriodTest()
        {
            string id = "d1";
            var identity = Mock.Of<IIdentity>(i => i.Id == id);

            var desired2 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p2"] = "v2",
                ["p3"] = "v3",
                ["$version"] = 2
            };

            var reported2 = new PropertyCollection
            {
                ["p2"] = "vp2",
                ["$version"] = 2
            };

            var twin2 = new TwinProperties
            {
                Reported = reported2,
                Desired = desired2
            };

            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(Option.Some(twin2));

            TwinProperties storedTwin = null;
            twinStore.Setup(c => c.Update(id, It.IsAny<TwinProperties>()))
                .Callback<string, TwinProperties>((s, t) => storedTwin = t)
                .Returns(Task.CompletedTask);

            var reportedPropertiesStore = new Mock<IReportedPropertiesStore>(MockBehavior.Strict);
            reportedPropertiesStore.Setup(r => r.SyncToCloud(id))
                .Returns(Task.CompletedTask);

            var deviceProxy = new Mock<IDeviceProxy>(MockBehavior.Strict);

            var cloudSync = new Mock<ICloudSync>(MockBehavior.Strict);
            cloudSync.Setup(c => c.GetTwin(id))
                .ReturnsAsync(Option.Some(twin2));
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionConverter = new PropertyCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<PropertyCollection>>();

            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.CheckClientSubscription(id, DeviceSubscription.DesiredPropertyUpdates)
                    && c.GetDeviceConnection(id) == Option.Some(deviceProxy.Object)
                    && c.GetConnectedClients() == new[] { identity });

            var deviceConnectivityManager = new Mock<IDeviceConnectivityManager>();

            var twinManager = new StoringTwinManager(
                connectionManager,
                twinCollectionConverter,
                twinMessageConverter,
                reportedPropertiesValidator,
                twinStore.Object,
                reportedPropertiesStore.Object,
                cloudSync.Object,
                deviceConnectivityManager.Object,
                TimeSpan.FromMinutes(10));

            // Act
            IMessage getTwinMessage = await twinManager.GetTwinAsync(id);

            // Assert
            Assert.NotNull(getTwinMessage);
            TwinProperties getTwin = twinMessageConverter.FromMessage(getTwinMessage);
            Assert.NotNull(getTwin);
            Assert.Equal(twin2.Desired.GetSerializedString(), getTwin.Desired.GetSerializedString());
            Assert.Equal(twin2.Reported.GetSerializedString(), getTwin.Reported.GetSerializedString());

            // Act
            deviceConnectivityManager.Raise(d => d.DeviceConnected += null, this, new EventArgs());

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(3));
            twinStore.VerifyAll();
            reportedPropertiesStore.VerifyAll();
            deviceProxy.VerifyAll();
            cloudSync.Verify(c => c.GetTwin(id), Times.AtMostOnce);

            Assert.NotNull(storedTwin);
            Assert.Equal(twin2.Desired.GetSerializedString(), storedTwin.Desired.GetSerializedString());
            Assert.Equal(twin2.Reported.GetSerializedString(), storedTwin.Reported.GetSerializedString());
        }
    }
}
