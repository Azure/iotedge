// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Core.Constants;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;

    [Unit]
    public class EdgeHubConnectionTest
    {
        [Theory]
        [InlineData("1.0", null)]
        [InlineData("1.1", null)]
        [InlineData("1.2", null)]
        [InlineData("1.3", null)]
        [InlineData("1", typeof(ArgumentException))]
        [InlineData("", typeof(ArgumentException))]
        [InlineData(null, typeof(ArgumentException))]
        [InlineData("0.1", typeof(InvalidOperationException))]
        [InlineData("2.0", typeof(InvalidOperationException))]
        [InlineData("2.1", typeof(InvalidOperationException))]
        public void SchemaVersionCheckTest(string schemaVersion, Type expectedException)
        {
            if (expectedException != null)
            {
                Assert.Throws(expectedException, () => EdgeHubConnection.ValidateSchemaVersion(schemaVersion));
            }
            else
            {
                EdgeHubConnection.ValidateSchemaVersion(schemaVersion);
            }
        }

        [Fact]
        public async Task HandleMethodInvocationTest()
        {
            // Arrange
            var edgeHubIdentity = Mock.Of<IModuleIdentity>();
            var twinManager = Mock.Of<ITwinManager>();
            var routeFactory = new EdgeRouteFactory(Mock.Of<IEndpointFactory>());
            var twinCollectionMessageConverter = Mock.Of<Core.IMessageConverter<TwinCollection>>();
            var twinMessageConverter = Mock.Of<Core.IMessageConverter<Shared.Twin>>();
            var versionInfo = new VersionInfo("1.0", "1", "123");
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            deviceScopeIdentitiesCache.Setup(d => d.RefreshServiceIdentities(It.IsAny<IEnumerable<string>>())).Returns(Task.CompletedTask);
            var edgeHubConnection = new EdgeHubConnection(edgeHubIdentity, twinManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter, versionInfo, deviceScopeIdentitiesCache.Object);

            string correlationId = Guid.NewGuid().ToString();
            var requestPayload = new
            {
                deviceIds = new[]
                {
                    "d1",
                    "d2"
                }
            };
            byte[] requestBytes = requestPayload.ToBytes();
            var directMethodRequest = new DirectMethodRequest(correlationId, Constants.ServiceIdentityRefreshMethodName, requestBytes, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            // Act
            DirectMethodResponse directMethodResponse = await edgeHubConnection.HandleMethodInvocation(directMethodRequest);

            // Assert
            Assert.NotNull(directMethodResponse);
            Assert.Equal(HttpStatusCode.OK, directMethodResponse.HttpStatusCode);
        }

        [Fact]
        public async Task HandleMethodInvocationBadInputTest()
        {
            // Arrange
            var edgeHubIdentity = Mock.Of<IModuleIdentity>();
            var twinManager = Mock.Of<ITwinManager>();
            var routeFactory = new EdgeRouteFactory(Mock.Of<IEndpointFactory>());
            var twinCollectionMessageConverter = Mock.Of<Core.IMessageConverter<TwinCollection>>();
            var twinMessageConverter = Mock.Of<Core.IMessageConverter<Shared.Twin>>();
            var versionInfo = new VersionInfo("1.0", "1", "123");
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            deviceScopeIdentitiesCache.Setup(d => d.RefreshServiceIdentities(It.IsAny<IEnumerable<string>>())).Returns(Task.CompletedTask);
            var edgeHubConnection = new EdgeHubConnection(edgeHubIdentity, twinManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter, versionInfo, deviceScopeIdentitiesCache.Object);

            string correlationId = Guid.NewGuid().ToString();
            var requestPayload = new[]
            {
                "d1",
                "d2"
            };
            byte[] requestBytes = requestPayload.ToBytes();
            var directMethodRequest = new DirectMethodRequest(correlationId, Constants.ServiceIdentityRefreshMethodName, requestBytes, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            // Act
            DirectMethodResponse directMethodResponse = await edgeHubConnection.HandleMethodInvocation(directMethodRequest);

            // Assert
            Assert.NotNull(directMethodResponse);
            Assert.Equal(HttpStatusCode.BadRequest, directMethodResponse.HttpStatusCode);
        }

        [Fact]
        public async Task HandleMethodInvocationServerErrorTest()
        {
            // Arrange
            var edgeHubIdentity = Mock.Of<IModuleIdentity>();
            var twinManager = Mock.Of<ITwinManager>();
            var routeFactory = new EdgeRouteFactory(Mock.Of<IEndpointFactory>());
            var twinCollectionMessageConverter = Mock.Of<Core.IMessageConverter<TwinCollection>>();
            var twinMessageConverter = Mock.Of<Core.IMessageConverter<Shared.Twin>>();
            var versionInfo = new VersionInfo("1.0", "1", "123");
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            deviceScopeIdentitiesCache.Setup(d => d.RefreshServiceIdentities(It.IsAny<IEnumerable<string>>())).ThrowsAsync(new Exception("Foo"));
            var edgeHubConnection = new EdgeHubConnection(edgeHubIdentity, twinManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter, versionInfo, deviceScopeIdentitiesCache.Object);

            string correlationId = Guid.NewGuid().ToString();
            var requestPayload = new
            {
                deviceIds = new[]
                {
                    "d1",
                    "d2"
                }
            };
            byte[] requestBytes = requestPayload.ToBytes();
            var directMethodRequest = new DirectMethodRequest(correlationId, Constants.ServiceIdentityRefreshMethodName, requestBytes, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            // Act
            DirectMethodResponse directMethodResponse = await edgeHubConnection.HandleMethodInvocation(directMethodRequest);

            // Assert
            Assert.NotNull(directMethodResponse);
            Assert.Equal(HttpStatusCode.InternalServerError, directMethodResponse.HttpStatusCode);
        }

        [Fact]
        public async Task HandleMethodInvocationInvalidMethodNameTest()
        {
            // Arrange
            var edgeHubIdentity = Mock.Of<IModuleIdentity>();
            var twinManager = Mock.Of<ITwinManager>();
            var routeFactory = new EdgeRouteFactory(Mock.Of<IEndpointFactory>());
            var twinCollectionMessageConverter = Mock.Of<Core.IMessageConverter<TwinCollection>>();
            var twinMessageConverter = Mock.Of<Core.IMessageConverter<Shared.Twin>>();
            var versionInfo = new VersionInfo("1.0", "1", "123");
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            deviceScopeIdentitiesCache.Setup(d => d.RefreshServiceIdentities(It.IsAny<IEnumerable<string>>())).Returns(Task.CompletedTask);
            var edgeHubConnection = new EdgeHubConnection(edgeHubIdentity, twinManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter, versionInfo, deviceScopeIdentitiesCache.Object);

            string correlationId = Guid.NewGuid().ToString();
            var requestPayload = new
            {
                deviceIds = new[]
                {
                    "d1",
                    "d2"
                }
            };
            byte[] requestBytes = requestPayload.ToBytes();
            var directMethodRequest = new DirectMethodRequest(correlationId, "ping", requestBytes, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            // Act
            DirectMethodResponse directMethodResponse = await edgeHubConnection.HandleMethodInvocation(directMethodRequest);

            // Assert
            Assert.NotNull(directMethodResponse);
            Assert.Equal(HttpStatusCode.NotFound, directMethodResponse.HttpStatusCode);
        }

        [Fact]
        public async Task HandleDeviceConnectedDisconnectedEvents()
        {
            // Arrange
            var edgeHubIdentity = Mock.Of<IModuleIdentity>(i => i.Id == "edgeD1/$edgeHub");
            var twinManager = Mock.Of<ITwinManager>();
            var routeFactory = new EdgeRouteFactory(Mock.Of<IEndpointFactory>());
            var twinCollectionMessageConverter = new TwinCollectionMessageConverter();
            var twinMessageConverter = new TwinMessageConverter();
            var versionInfo = new VersionInfo("1.0", "1", "123");
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();
            var edgeHubConnection = new EdgeHubConnection(edgeHubIdentity, twinManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter, versionInfo, deviceScopeIdentitiesCache);

            Mock.Get(twinManager).Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<string>(), It.IsAny<IMessage>())).Returns(Task.CompletedTask);

            // Act
            edgeHubConnection.DeviceConnected(this, Mock.Of<IIdentity>(i => i.Id == "d1"));
            edgeHubConnection.DeviceConnected(this, Mock.Of<IIdentity>(i => i.Id == "edgeD1/$edgeHub"));

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(2));
            Mock.Get(twinManager).Verify(t => t.UpdateReportedPropertiesAsync(It.IsAny<string>(), It.IsAny<IMessage>()), Times.Once);

            // Act
            edgeHubConnection.DeviceDisconnected(this, Mock.Of<IIdentity>(i => i.Id == "d1"));
            edgeHubConnection.DeviceDisconnected(this, Mock.Of<IIdentity>(i => i.Id == "edgeD1/$edgeHub"));

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(2));
            Mock.Get(twinManager).Verify(t => t.UpdateReportedPropertiesAsync(It.IsAny<string>(), It.IsAny<IMessage>()), Times.Exactly(2));
        }
    }
}
