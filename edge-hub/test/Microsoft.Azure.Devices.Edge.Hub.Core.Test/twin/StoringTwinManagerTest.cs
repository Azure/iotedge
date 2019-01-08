// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.twin
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
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

            var twinManager = new StoringTwinManager(
                connectionManager,
                twinCollectionConverter,
                twinMessageConverter,
                reportedPropertiesValidator,
                twinStore.Object,
                reportedPropertiesStore,
                cloudSync.Object,
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

            var twinManager = new StoringTwinManager(
                connectionManager,
                twinCollectionConverter,
                twinMessageConverter,
                reportedPropertiesValidator,
                twinStore.Object,
                reportedPropertiesStore.Object,
                cloudSync,
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
        public Task UpdateDesiredPropertiesTest()
        {
            string id = "d1";

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
                .ReturnsAsync(Option.None<Twin>());

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

            var twinManager = new StoringTwinManager(
                connectionManager,
                twinCollectionConverter,
                twinMessageConverter,
                reportedPropertiesValidator,
                twinStore.Object,
                reportedPropertiesStore.Object,
                cloudSync,
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
    }
}
