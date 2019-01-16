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
    using Microsoft.Azure.Devices.Shared;
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

            var desired1 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            var reported1 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            var twin1 = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = desired1,
                    Reported = reported1
                }
            };

            var cloudSync = new Mock<ICloudSync>();
            cloudSync.SetupSequence(c => c.GetTwin(id))
                .ReturnsAsync(Option.None<Twin>())
                .ReturnsAsync(Option.Some(twin1))
                .ReturnsAsync(Option.None<Twin>());

            Twin receivedTwin = null;
            var twinStore = new Mock<ITwinStore>();
            twinStore.Setup(c => c.Update(id, It.IsAny<Twin>()))
                .Callback<string, Twin>((s, t) => receivedTwin = t)
                .Returns(Task.CompletedTask);

            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(() => Option.Maybe(receivedTwin));

            var twinMessageConverter = new TwinMessageConverter();
            var connectionManager = Mock.Of<IConnectionManager>();
            var twinCollectionConverter = Mock.Of<IMessageConverter<TwinCollection>>();
            var reportedPropertiesValidator = Mock.Of<IValidator<TwinCollection>>();
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
            Twin twin = twinMessageConverter.FromMessage(twinMessage);
            Assert.NotNull(twin);
            Assert.Equal("{\"deviceId\":null,\"etag\":null,\"version\":null,\"properties\":{\"desired\":{},\"reported\":{}}}", twin.ToJson());

            // Act
            twinMessage = await twinManager.GetTwinAsync(id);

            // Assert
            Assert.NotNull(twinMessage);
            twin = twinMessageConverter.FromMessage(twinMessage);
            Assert.NotNull(twin);
            Assert.NotNull(receivedTwin);
            Assert.Equal(receivedTwin.ToJson(), twin.ToJson());
            Assert.Equal(twin1.ToJson(), twin.ToJson());

            // Act
            twinMessage = await twinManager.GetTwinAsync(id);

            // Assert
            Assert.NotNull(twinMessage);
            twin = twinMessageConverter.FromMessage(twinMessage);
            Assert.NotNull(twin);
            Assert.NotNull(receivedTwin);
            Assert.Equal(receivedTwin.ToJson(), twin.ToJson());
            Assert.Equal(twin1.ToJson(), twin.ToJson());
        }

        [Fact]
        public async Task UpdateReportedPropertiesTest()
        {
            string id = "d1";

            var reported1 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3"
            };

            TwinCollection receivedTwinPatch = null;
            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.UpdateReportedProperties(id, It.IsAny<TwinCollection>()))
                .Callback<string, TwinCollection>((s, t) => receivedTwinPatch = t)
                .Returns(Task.CompletedTask);

            TwinCollection receivedTwinPatch2 = null;
            var reportedPropertiesStore = new Mock<IReportedPropertiesStore>(MockBehavior.Strict);
            reportedPropertiesStore.Setup(r => r.InitSyncToCloud(id));
            reportedPropertiesStore.Setup(r => r.Update(id, It.IsAny<TwinCollection>()))
                .Callback<string, TwinCollection>((s, t) => receivedTwinPatch2 = t)
                .Returns(Task.CompletedTask);

            var cloudSync = Mock.Of<ICloudSync>();
            var twinMessageConverter = new TwinMessageConverter();
            var connectionManager = Mock.Of<IConnectionManager>();
            var twinCollectionConverter = new TwinCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<TwinCollection>>();
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
            Assert.Equal(reported1.ToJson(), receivedTwinPatch.ToJson());
            Assert.Equal(reported1.ToJson(), receivedTwinPatch2.ToJson());
        }

        [Fact]
        public async Task UpdateDesiredPropertiesTest()
        {
            string id = "d1";

            var desired0 = new TwinCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var reported0 = new TwinCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var twinBase = new Twin
            {
                Properties = new TwinProperties
                {
                    Reported = reported0,
                    Desired = desired0
                }
            };

            var desired1 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            TwinCollection receivedTwinPatch = null;
            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.UpdateDesiredProperties(id, It.IsAny<TwinCollection>()))
                .Callback<string, TwinCollection>((s, t) => receivedTwinPatch = t)
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
            var twinCollectionConverter = new TwinCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<TwinCollection>>();
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
            Assert.Equal(desired1.ToJson(), receivedTwinPatch.ToJson());
            TwinCollection receivedTwinPatch2 = twinCollectionConverter.FromMessage(receivedTwinPatchMessage);
            Assert.Equal(desired1.ToJson(), receivedTwinPatch2.ToJson());
        }

        [Fact]
        public async Task UpdateDesiredPropertiesWithIncorrectPatchTest()
        {
            string id = "d1";

            var desired0 = new TwinCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var reported0 = new TwinCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var twinBase = new Twin
            {
                Properties = new TwinProperties
                {
                    Reported = reported0,
                    Desired = desired0
                }
            };

            var desired2 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p2"] = "v2",
                ["p3"] = "v3",
                ["$version"] = 2
            };

            var reported2 = new TwinCollection
            {
                ["p2"] = "vp2",
                ["$version"] = 2
            };

            var twin2 = new Twin
            {
                Properties = new TwinProperties
                {
                    Reported = reported2,
                    Desired = desired2
                }
            };

            var desired2Patch = new TwinCollection
            {
                ["p1"] = "vp1",
                ["$version"] = 2
            };

            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(Option.Some(twinBase));

            Twin storedTwin = null;
            twinStore.Setup(c => c.Update(id, It.IsAny<Twin>()))
                .Callback<string, Twin>((s, t) => storedTwin = t)
                .Returns(Task.CompletedTask);

            var reportedPropertiesStore = new Mock<IReportedPropertiesStore>(MockBehavior.Strict);

            IMessage receivedTwinPatchMessage = null;
            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedTwinPatchMessage = m)
                .Returns(Task.CompletedTask);

            var cloudSync = Mock.Of<ICloudSync>(c => c.GetTwin(id) == Task.FromResult(Option.Some(twin2)));
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionConverter = new TwinCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<TwinCollection>>();
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
            Assert.Equal(twin2.ToJson(), storedTwin.ToJson());
            TwinCollection receivedTwinPatch2 = twinCollectionConverter.FromMessage(receivedTwinPatchMessage);
            Assert.Equal("{\"p0\":null,\"$version\":2,\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":\"v3\"}", receivedTwinPatch2.ToJson());
        }

        [Fact]
        public async Task DeviceConnectionTest()
        {
            string id = "d1";
            var identity = Mock.Of<IIdentity>(i => i.Id == id);

            var desired0 = new TwinCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var reported0 = new TwinCollection
            {
                ["p0"] = "vp0",
                ["$version"] = 0
            };

            var twinBase = new Twin
            {
                Properties = new TwinProperties
                {
                    Reported = reported0,
                    Desired = desired0
                }
            };

            var desired2 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p2"] = "v2",
                ["p3"] = "v3",
                ["$version"] = 2
            };

            var reported2 = new TwinCollection
            {
                ["p2"] = "vp2",
                ["$version"] = 2
            };

            var twin2 = new Twin
            {
                Properties = new TwinProperties
                {
                    Reported = reported2,
                    Desired = desired2
                }
            };

            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(Option.Some(twinBase));

            Twin storedTwin = null;
            twinStore.Setup(c => c.Update(id, It.IsAny<Twin>()))
                .Callback<string, Twin>((s, t) => storedTwin = t)
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
            var twinCollectionConverter = new TwinCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<TwinCollection>>();

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
            Assert.Equal(twin2.ToJson(), storedTwin.ToJson());
            TwinCollection receivedTwinPatch2 = twinCollectionConverter.FromMessage(receivedTwinPatchMessage);
            Assert.Equal("{\"p0\":null,\"$version\":2,\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":\"v3\"}", receivedTwinPatch2.ToJson());
        }

        [Fact]
        public async Task DeviceConnectionNoSubscriptionTest()
        {
            string id = "d1";
            var identity = Mock.Of<IIdentity>(i => i.Id == id);

            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(Option.None<Twin>());

            var reportedPropertiesStore = new Mock<IReportedPropertiesStore>(MockBehavior.Strict);
            reportedPropertiesStore.Setup(r => r.SyncToCloud(id))
                .Returns(Task.CompletedTask);

            var deviceProxy = new Mock<IDeviceProxy>(MockBehavior.Strict);
            var cloudSync = new Mock<ICloudSync>(MockBehavior.Strict);
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionConverter = new TwinCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<TwinCollection>>();

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

            var desired2 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p2"] = "v2",
                ["p3"] = "v3",
                ["$version"] = 2
            };

            var reported2 = new TwinCollection
            {
                ["p2"] = "vp2",
                ["$version"] = 2
            };

            var twin2 = new Twin
            {
                Properties = new TwinProperties
                {
                    Reported = reported2,
                    Desired = desired2
                }
            };

            var twinStore = new Mock<ITwinStore>(MockBehavior.Strict);
            twinStore.Setup(c => c.Get(id))
                .ReturnsAsync(Option.Some(twin2));

            Twin storedTwin = null;
            twinStore.Setup(c => c.Update(id, It.IsAny<Twin>()))
                .Callback<string, Twin>((s, t) => storedTwin = t)
                .Returns(Task.CompletedTask);

            var reportedPropertiesStore = new Mock<IReportedPropertiesStore>(MockBehavior.Strict);
            reportedPropertiesStore.Setup(r => r.SyncToCloud(id))
                .Returns(Task.CompletedTask);

            var deviceProxy = new Mock<IDeviceProxy>(MockBehavior.Strict);

            var cloudSync = new Mock<ICloudSync>(MockBehavior.Strict);
            cloudSync.Setup(c => c.GetTwin(id))
                .ReturnsAsync(Option.Some(twin2));
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionConverter = new TwinCollectionMessageConverter();
            var reportedPropertiesValidator = Mock.Of<IValidator<TwinCollection>>();

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
            Twin getTwin = twinMessageConverter.FromMessage(getTwinMessage);
            Assert.NotNull(getTwin);
            Assert.Equal(twin2.ToJson(), getTwin.ToJson());

            // Act
            deviceConnectivityManager.Raise(d => d.DeviceConnected += null, this, new EventArgs());

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(3));
            twinStore.VerifyAll();
            reportedPropertiesStore.VerifyAll();
            deviceProxy.VerifyAll();
            cloudSync.Verify(c => c.GetTwin(id), Times.AtMostOnce);

            Assert.NotNull(storedTwin);
            Assert.Equal(twin2.ToJson(), storedTwin.ToJson());
        }
    }
}
