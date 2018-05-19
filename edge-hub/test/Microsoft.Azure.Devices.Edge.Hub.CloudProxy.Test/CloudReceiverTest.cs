// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class CloudReceiverTest
    {
        const string MethodName = "MethodName";
        const string RequestId = "1";
        const int StatusCode = 200;
        static readonly byte[] Data = new byte[0];

        [Fact]
        [Unit]
        public async Task MethodCallHandler_WhenResponse_WithRequestIdReceived_Completes()
        {
            var cloudListener = new Mock<ICloudListener>();
            cloudListener.Setup(p => p.CallMethodAsync(It.IsAny<DirectMethodRequest>())).Returns(Task.FromResult(new DirectMethodResponse(RequestId, Data, StatusCode)));
            var messageConverter = new Mock<IMessageConverterProvider>();
            var identity = Mock.Of<IIdentity>(i => i.Id == "device1");

            var deviceClient = Mock.Of<IClient>();
            var cloudProxy = new CloudProxy(deviceClient, messageConverter.Object, identity.Id, (id, s) => { });

            var cloudReceiver = new CloudProxy.CloudReceiver(cloudProxy, cloudListener.Object);

            MethodResponse methodResponse = await cloudReceiver.MethodCallHandler(new MethodRequest(MethodName, Data), null);
            cloudListener.Verify(p => p.CallMethodAsync(It.Is<DirectMethodRequest>(x => x.Name == MethodName && x.Data == Data)), Times.Once);
            Assert.NotNull(methodResponse);
        }

        [Fact]
        [Unit]
        public void ConstructorWithNullCloudProxyShallThrow()
        {
            //Arrange
            var cloudListener = Mock.Of<ICloudListener>();
                
            //Act
            //Assert
            Assert.Throws<ArgumentNullException>(() => new CloudProxy.CloudReceiver(null, cloudListener));
        }

        [Fact]
        [Unit]
        public void ConstructorWithNullCloudListenerThrow()
        {
            //Arrange
            var messageConverter = new Mock<IMessageConverterProvider>();
            var deviceClient = Mock.Of<IClient>();
            var cloudProxy = new CloudProxy(deviceClient, messageConverter.Object, "device1", (id, s) => { });

            //Act
            //Assert
            Assert.Throws<ArgumentNullException>(() => new CloudProxy.CloudReceiver(cloudProxy, null));
        }

        [Fact]
        [Unit]
        public void ConstructorHappyPath()
        {
            //Arrange
            var messageConverter = new Mock<IMessageConverterProvider>();
            var identity = Mock.Of<IIdentity>(i => i.Id == "device1");
            var deviceClient = Mock.Of<IClient>();
            var cloudProxy = new CloudProxy(deviceClient, messageConverter.Object, identity.Id, (id, s) => { });
            var cloudListener = new Mock<ICloudListener>();

            //Act
            var cloudReceive = new CloudProxy.CloudReceiver(cloudProxy, cloudListener.Object);

            //Assert
            Assert.NotNull(cloudReceive);
        }

        [Fact]
        [Unit]
        public void CloudReceiverCanProcessAMessage()
        {
            //Arrange
            var sampleMessage = Mock.Of<IMessage>();

            var messageConverter = new  Mock<IMessageConverter<Client.Message>>();
            messageConverter.Setup(p => p.ToMessage(It.IsAny<Client.Message>())).Returns(sampleMessage);

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(p => p.Get<Client.Message>()).Returns(messageConverter.Object);

            var identity = Mock.Of<IIdentity>(i => i.Id == "device1");
            var deviceClient = new Mock<IClient>();
            deviceClient.Setup(p => p.ReceiveAsync(It.IsAny<TimeSpan>())).ReturnsAsync(new Client.Message(new byte[0]));

            var cloudProxy = new CloudProxy(deviceClient.Object, messageConverterProvider.Object, identity.Id, (id, s) => { });
            var cloudListener = new Mock<ICloudListener>();

            var cloudReceiver = new CloudProxy.CloudReceiver(cloudProxy, cloudListener.Object);

            cloudListener.Setup(p => p.ProcessMessageAsync(It.IsAny<IMessage>()))
                .Returns(Task.CompletedTask)
                .Callback(() => cloudReceiver.CloseAsync());

            //Act
            cloudReceiver.StartListening();

            //Assert
            cloudListener.Verify(m => m.ProcessMessageAsync(sampleMessage));
        }

        [Fact]
        [Unit]
        public void CloudReceiverDisconnectsWhenItReceivesUnauthorizedException()
        {
            //Arrange
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();

            var deviceClient = new Mock<IClient>();
            deviceClient.Setup(p => p.ReceiveAsync(It.IsAny<TimeSpan>()))
                .Throws(new UnauthorizedException("")); 

            var cloudProxy = new CloudProxy(deviceClient.Object, messageConverterProvider, "device1", (id, s) => { });
            var cloudListener = Mock.Of<ICloudListener>();

            var cloudReceiver = new CloudProxy.CloudReceiver(cloudProxy, cloudListener);

            //Act
            cloudReceiver.StartListening(); //Exception expected to be handled and not thrown.

            //Assert
            //The verification is implicit, just making sure the exception is handled.
        }

        [Fact]
        [Unit]
        public void CloudReceiverIgnoresOtherExceptionsWhenReceivingMessages()
        {
            //Arrange
            var messageConverter = new Mock<IMessageConverter<Client.Message>>();
            messageConverter.Setup(p => p.ToMessage(It.IsAny<Client.Message>())).Returns<IMessage>(null);

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(p => p.Get<Client.Message>()).Returns(messageConverter.Object);

            var identity = Mock.Of<IIdentity>(i => i.Id == "device1");
            var deviceClient = new Mock<IClient>();
            deviceClient.Setup(p => p.ReceiveAsync(It.IsAny<TimeSpan>())).ReturnsAsync(new Client.Message(new byte[0]));

            var cloudProxy = new CloudProxy(deviceClient.Object, messageConverterProvider.Object, identity.Id, (id, s) => { });
            var cloudListener = new Mock<ICloudListener>();

            var cloudReceiver = new CloudProxy.CloudReceiver(cloudProxy, cloudListener.Object);

            cloudListener.Setup(p => p.ProcessMessageAsync(It.IsAny<IMessage>()))
                .Callback(() => cloudReceiver.CloseAsync())
                .Throws(new Exception());

            //Act
            cloudReceiver.StartListening(); //Exception expected to be handled and not thrown.

            //Assert
            cloudListener.Verify(m => m.ProcessMessageAsync(null));
        }


        [Fact]
        [Unit]
        public void SetupDesiredPropertyUpdatesAsync()
        {
            //Arrange
            var messageConverterProvider = new Mock<IMessageConverterProvider>();

            var identity = Mock.Of<IIdentity>(i => i.Id == "device1");
            var deviceClient = new Mock<IClient>();

            var cloudProxy = new CloudProxy(deviceClient.Object, messageConverterProvider.Object, identity.Id, (id, s) => { });
            var cloudListener = new Mock<ICloudListener>();

            var cloudReceiver = new CloudProxy.CloudReceiver(cloudProxy, cloudListener.Object);

            //Act
            cloudReceiver.SetupDesiredPropertyUpdatesAsync();

            //Assert
            deviceClient.Verify(m => m.SetDesiredPropertyUpdateCallbackAsync(It.IsAny<DesiredPropertyUpdateCallback>(), It.IsAny<object>()));
        }

        [Fact]
        [Unit]
        public void RemoveDesiredPropertyUpdatesAsync()
        {
            //Arrange
            var messageConverterProvider = new Mock<IMessageConverterProvider>();

            var identity = Mock.Of<IIdentity>(i => i.Id == "device1");
            var deviceClient = new Mock<IClient>();
            deviceClient.Setup(dc => dc.SetDesiredPropertyUpdateCallbackAsync(null, null)).Throws(new Exception("Update this test!")); //This is to catch onde the TODO on the code get's in.
  

            var cloudProxy = new CloudProxy(deviceClient.Object, messageConverterProvider.Object, identity.Id, (id, s) => { });
            var cloudListener = new Mock<ICloudListener>();

            var cloudReceiver = new CloudProxy.CloudReceiver(cloudProxy, cloudListener.Object);

            //Act
            cloudReceiver.RemoveDesiredPropertyUpdatesAsync();

            //Assert

        }
    }
}
