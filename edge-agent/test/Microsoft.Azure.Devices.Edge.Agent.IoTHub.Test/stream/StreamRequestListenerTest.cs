// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test.Stream
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class StreamRequestListenerTest
    {
        [Fact]
        public async Task HandleRequestTest()
        {
            // Arrange
            var deviceStreamRequest1 = new DeviceStreamRequest("req1", "Type1", new Uri("http://dummyurl"), Guid.NewGuid().ToString());
            var deviceStreamRequest2 = new DeviceStreamRequest("req2", "Type2", new Uri("http://dummyurl"), Guid.NewGuid().ToString());
            var deviceStreamRequest3 = new DeviceStreamRequest("req3", "Type3", new Uri("http://dummyurl"), Guid.NewGuid().ToString());

            var clientWebSocket1 = new Mock<IClientWebSocket>();
            var clientWebSocket2 = new Mock<IClientWebSocket>();
            var clientWebSocket3 = new Mock<IClientWebSocket>();

            var moduleClient = new Mock<IModuleClient>();
            moduleClient.SetupSequence(m => m.WaitForDeviceStreamRequestAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(deviceStreamRequest1)
                .ReturnsAsync(deviceStreamRequest2)
                .ReturnsAsync(deviceStreamRequest3);

            moduleClient.Setup(m => m.AcceptDeviceStreamingRequestAndConnect(deviceStreamRequest1, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(clientWebSocket1.Object));
            moduleClient.Setup(m => m.AcceptDeviceStreamingRequestAndConnect(deviceStreamRequest2, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(clientWebSocket2.Object));
            moduleClient.Setup(m => m.AcceptDeviceStreamingRequestAndConnect(deviceStreamRequest3, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(clientWebSocket3.Object));

            var requestHandler1TaskCompletionSource = new TaskCompletionSource<bool>();
            var streamRequestHandlerType1Mock = new Mock<IStreamRequestHandler>();
            streamRequestHandlerType1Mock.Setup(s => s.Handle(clientWebSocket1.Object, It.IsAny<CancellationToken>()))
                .Returns(requestHandler1TaskCompletionSource.Task);

            var requestHandler2TaskCompletionSource = new TaskCompletionSource<bool>();
            var streamRequestHandlerType2Mock = new Mock<IStreamRequestHandler>();
            streamRequestHandlerType2Mock.Setup(s => s.Handle(clientWebSocket2.Object, It.IsAny<CancellationToken>()))
                .Returns(requestHandler2TaskCompletionSource.Task);

            var requestHandler3TaskCompletionSource = new TaskCompletionSource<bool>();
            var streamRequestHandlerType3Mock = new Mock<IStreamRequestHandler>();
            streamRequestHandlerType3Mock.Setup(s => s.Handle(clientWebSocket3.Object, It.IsAny<CancellationToken>()))
                .Returns(requestHandler3TaskCompletionSource.Task);

            IStreamRequestHandler streamRequestHandlerType1 = streamRequestHandlerType1Mock.Object;
            IStreamRequestHandler streamRequestHandlerType2 = streamRequestHandlerType2Mock.Object;
            IStreamRequestHandler streamRequestHandlerType3 = streamRequestHandlerType3Mock.Object;

            var streamRequestHandlerProvider = new Mock<IStreamRequestHandlerProvider>();
            streamRequestHandlerProvider.Setup(s => s.TryGetHandler("Type1", out streamRequestHandlerType1)).Returns(true);
            streamRequestHandlerProvider.Setup(s => s.TryGetHandler("Type2", out streamRequestHandlerType2)).Returns(true);
            streamRequestHandlerProvider.Setup(s => s.TryGetHandler("Type3", out streamRequestHandlerType3)).Returns(true);

            var streamRequestListener = new StreamRequestListener(streamRequestHandlerProvider.Object, 2);

            // Act
            streamRequestListener.InitPump(moduleClient.Object);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));

            streamRequestHandlerType1Mock.Verify(s => s.Handle(clientWebSocket1.Object, It.IsAny<CancellationToken>()), Times.Once);
            streamRequestHandlerType2Mock.Verify(s => s.Handle(clientWebSocket2.Object, It.IsAny<CancellationToken>()), Times.Once);
            streamRequestHandlerType3Mock.Verify(s => s.Handle(clientWebSocket3.Object, It.IsAny<CancellationToken>()), Times.Never);

            requestHandler1TaskCompletionSource.SetResult(true);
            await Task.Delay(TimeSpan.FromSeconds(5));

            streamRequestHandlerType1Mock.Verify(s => s.Handle(clientWebSocket1.Object, It.IsAny<CancellationToken>()), Times.Once);
            streamRequestHandlerType2Mock.Verify(s => s.Handle(clientWebSocket2.Object, It.IsAny<CancellationToken>()), Times.Once);
            streamRequestHandlerType3Mock.Verify(s => s.Handle(clientWebSocket3.Object, It.IsAny<CancellationToken>()), Times.Once);

            requestHandler2TaskCompletionSource.SetResult(true);
            requestHandler3TaskCompletionSource.SetResult(true);

            moduleClient.VerifyAll();
            streamRequestHandlerProvider.VerifyAll();
        }
    }
}
