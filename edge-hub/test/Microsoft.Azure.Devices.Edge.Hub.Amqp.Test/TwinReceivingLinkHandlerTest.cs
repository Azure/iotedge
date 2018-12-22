// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class TwinReceivingLinkHandlerTest
    {
        [Fact]
        public async Task ProcessPutOperationMessageTest()
        {
            // Arrange
            string receivedCorrelationId = null;
            var deviceListener = new Mock<IDeviceListener>();
            deviceListener.Setup(d => d.AddDesiredPropertyUpdatesSubscription(It.IsAny<string>()))
                .Callback<string>(c => receivedCorrelationId = c)
                .Returns(Task.CompletedTask);
            var connectionHandler = Mock.Of<IConnectionHandler>(c => c.GetDeviceListener() == Task.FromResult(deviceListener.Object));
            var amqpAuthenticator = new Mock<IAmqpAuthenticator>();
            amqpAuthenticator.Setup(c => c.AuthenticateAsync("d1")).ReturnsAsync(true);
            Mock<ICbsNode> cbsNodeMock = amqpAuthenticator.As<ICbsNode>();
            ICbsNode cbsNode = cbsNodeMock.Object;
            var amqpConnection = Mock.Of<IAmqpConnection>(
                c =>
                    c.FindExtension<IConnectionHandler>() == connectionHandler &&
                    c.FindExtension<ICbsNode>() == cbsNode);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var receivingLink = Mock.Of<IReceivingAmqpLink>(l => l.Session == amqpSession && l.IsReceiver && l.Settings == new AmqpLinkSettings() && l.State == AmqpObjectState.Opened);

            var requestUri = new Uri("amqps://foo.bar/devices/d1/twin");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var twinReceivingLinkHandler = new TwinReceivingLinkHandler(identity, receivingLink, requestUri, boundVariables, connectionHandler, messageConverter);

            string correlationId = Guid.NewGuid().ToString();
            AmqpMessage amqpMessage = AmqpMessage.Create();
            amqpMessage.MessageAnnotations.Map["operation"] = "PUT";
            amqpMessage.Properties.CorrelationId = correlationId;

            // Act
            await twinReceivingLinkHandler.OpenAsync(TimeSpan.FromSeconds(60));
            await twinReceivingLinkHandler.ProcessMessageAsync(amqpMessage);

            // Assert
            Assert.NotNull(receivedCorrelationId);
            Assert.Equal(correlationId, receivedCorrelationId);
            deviceListener.VerifyAll();
        }

        [Fact]
        public async Task ProcessDeleteOperationMessageTest()
        {
            // Arrange
            string receivedCorrelationId = null;
            var deviceListener = new Mock<IDeviceListener>();
            deviceListener.Setup(d => d.RemoveDesiredPropertyUpdatesSubscription(It.IsAny<string>()))
                .Callback<string>(c => receivedCorrelationId = c)
                .Returns(Task.CompletedTask);
            var connectionHandler = Mock.Of<IConnectionHandler>(c => c.GetDeviceListener() == Task.FromResult(deviceListener.Object));
            var amqpAuthenticator = new Mock<IAmqpAuthenticator>();
            amqpAuthenticator.Setup(c => c.AuthenticateAsync("d1")).ReturnsAsync(true);
            Mock<ICbsNode> cbsNodeMock = amqpAuthenticator.As<ICbsNode>();
            ICbsNode cbsNode = cbsNodeMock.Object;
            var amqpConnection = Mock.Of<IAmqpConnection>(
                c =>
                    c.FindExtension<IConnectionHandler>() == connectionHandler &&
                    c.FindExtension<ICbsNode>() == cbsNode);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var receivingLink = Mock.Of<IReceivingAmqpLink>(l => l.Session == amqpSession && l.IsReceiver && l.Settings == new AmqpLinkSettings() && l.State == AmqpObjectState.Opened);

            var requestUri = new Uri("amqps://foo.bar/devices/d1/twin");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var twinReceivingLinkHandler = new TwinReceivingLinkHandler(identity, receivingLink, requestUri, boundVariables, connectionHandler, messageConverter);

            string correlationId = Guid.NewGuid().ToString();
            AmqpMessage amqpMessage = AmqpMessage.Create();
            amqpMessage.MessageAnnotations.Map["operation"] = "DELETE";
            amqpMessage.Properties.CorrelationId = correlationId;

            // Act
            await twinReceivingLinkHandler.OpenAsync(TimeSpan.FromSeconds(60));
            await twinReceivingLinkHandler.ProcessMessageAsync(amqpMessage);

            // Assert
            Assert.NotNull(receivedCorrelationId);
            Assert.Equal(correlationId, receivedCorrelationId);
            deviceListener.VerifyAll();
        }
    }
}
