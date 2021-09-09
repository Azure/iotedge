// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class InvokeMethodHandlerTest
    {
        [Fact]
        public async Task InvokeMethodTest()
        {
            // Arrange
            var request = new DirectMethodRequest("d1", "poke", null, TimeSpan.FromSeconds(10));

            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>()))
                .ReturnsAsync(() => new DirectMethodResponse(request.CorrelationId, null, 200));

            var deviceSubscriptions = new Dictionary<DeviceSubscription, bool>
            {
                [DeviceSubscription.Methods] = true
            };

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetDeviceConnection(It.IsAny<string>())).Returns(Option.Some(deviceProxy.Object));
            connectionManager.Setup(c => c.GetSubscriptions(It.IsAny<string>()))
                .Returns(Option.Some(new ReadOnlyDictionary<DeviceSubscription, bool>(deviceSubscriptions) as IReadOnlyDictionary<DeviceSubscription, bool>));

            IInvokeMethodHandler invokeMethodHandler = new InvokeMethodHandler(connectionManager.Object);

            // Act
            DirectMethodResponse response = await invokeMethodHandler.InvokeMethod(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(response.CorrelationId, request.CorrelationId);
            Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
            Assert.Null(response.Data);
            Assert.Equal((int)HttpStatusCode.OK, response.Status);
            Assert.False(response.Exception.HasValue);
        }

        [Fact]
        public async Task InvokeMethodClientTimesOutTest()
        {
            // Arrange
            var request = new DirectMethodRequest("d1", "poke", null, TimeSpan.FromSeconds(10));

            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>()))
                .ReturnsAsync(() => new DirectMethodResponse(new EdgeHubTimeoutException("Edge hub timed out"), HttpStatusCode.GatewayTimeout));

            var deviceSubscriptions = new Dictionary<DeviceSubscription, bool>
            {
                [DeviceSubscription.Methods] = true
            };

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetDeviceConnection(It.IsAny<string>())).Returns(Option.Some(deviceProxy.Object));
            connectionManager.Setup(c => c.GetSubscriptions(It.IsAny<string>()))
                .Returns(Option.Some(new ReadOnlyDictionary<DeviceSubscription, bool>(deviceSubscriptions) as IReadOnlyDictionary<DeviceSubscription, bool>));

            IInvokeMethodHandler invokeMethodHandler = new InvokeMethodHandler(connectionManager.Object);

            // Act
            DirectMethodResponse response = await invokeMethodHandler.InvokeMethod(request);

            // Assert
            Assert.NotNull(response);
            Assert.Null(response.CorrelationId);
            Assert.Equal(HttpStatusCode.GatewayTimeout, response.HttpStatusCode);
            Assert.Null(response.Data);
            Assert.Equal(0, response.Status);
            Assert.IsType<EdgeHubTimeoutException>(response.Exception.OrDefault());
        }

        [Fact]
        public async Task InvokeMethodDeviceNotConnectedTest()
        {
            // Arrange
            var request = new DirectMethodRequest("d1", "poke", null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60));

            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>()))
                .ReturnsAsync(() => new DirectMethodResponse(request.CorrelationId, null, 200));

            var deviceSubscriptions = new Dictionary<DeviceSubscription, bool>
            {
                [DeviceSubscription.Methods] = true
            };

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.SetupSequence(c => c.GetDeviceConnection(It.IsAny<string>()))
                .Returns(Option.None<IDeviceProxy>())
                .Returns(Option.Some(deviceProxy.Object));
            connectionManager.Setup(c => c.GetSubscriptions(It.IsAny<string>()))
                .Returns(Option.Some(new ReadOnlyDictionary<DeviceSubscription, bool>(deviceSubscriptions) as IReadOnlyDictionary<DeviceSubscription, bool>));

            IInvokeMethodHandler invokeMethodHandler = new InvokeMethodHandler(connectionManager.Object);

            // Act
            Task<DirectMethodResponse> invokeMethodTask = invokeMethodHandler.InvokeMethod(request);
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Assert
            Assert.False(invokeMethodTask.IsCompleted);

            // Act
            await invokeMethodHandler.ProcessInvokeMethodSubscription("d1");
            DirectMethodResponse response = await invokeMethodTask;

            // Assert
            Assert.NotNull(response);
            Assert.Equal(response.CorrelationId, request.CorrelationId);
            Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
            Assert.Null(response.Data);
            Assert.Equal((int)HttpStatusCode.OK, response.Status);
            Assert.False(response.Exception.HasValue);
        }

        [Fact]
        public async Task InvokeMethodNoSubscriptionTest()
        {
            // Arrange
            var request = new DirectMethodRequest("d1", "poke", null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60));

            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>()))
                .ReturnsAsync(() => new DirectMethodResponse(request.CorrelationId, null, 200));

            var deviceSubscriptions = new Dictionary<DeviceSubscription, bool>
            {
                [DeviceSubscription.Methods] = true
            };

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetDeviceConnection(It.IsAny<string>()))
                .Returns(Option.Some(deviceProxy.Object));
            connectionManager.SetupSequence(c => c.GetSubscriptions(It.IsAny<string>()))
                .Returns(Option.Some(new ReadOnlyDictionary<DeviceSubscription, bool>(new Dictionary<DeviceSubscription, bool>()) as IReadOnlyDictionary<DeviceSubscription, bool>))
                .Returns(Option.Some(new ReadOnlyDictionary<DeviceSubscription, bool>(deviceSubscriptions) as IReadOnlyDictionary<DeviceSubscription, bool>));

            IInvokeMethodHandler invokeMethodHandler = new InvokeMethodHandler(connectionManager.Object);

            // Act
            Task<DirectMethodResponse> invokeMethodTask = invokeMethodHandler.InvokeMethod(request);
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Assert
            Assert.False(invokeMethodTask.IsCompleted);

            // Act
            await invokeMethodHandler.ProcessInvokeMethodSubscription("d1");
            DirectMethodResponse response = await invokeMethodTask;

            // Assert
            Assert.NotNull(response);
            Assert.Equal(response.CorrelationId, request.CorrelationId);
            Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
            Assert.Null(response.Data);
            Assert.Equal((int)HttpStatusCode.OK, response.Status);
            Assert.False(response.Exception.HasValue);
        }
    }
}
