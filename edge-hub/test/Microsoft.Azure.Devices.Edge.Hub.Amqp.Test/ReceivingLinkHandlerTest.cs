// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ReceivingLinkHandlerTest
    {
        [Fact]
        public async Task ReceiveMessageTest()
        {
            // Arrange
            var deviceListener = new Mock<IDeviceListener>();
            var connectionHandler = Mock.Of<IConnectionHandler>(c => c.GetDeviceListener() == Task.FromResult(deviceListener.Object));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<IConnectionHandler>() == connectionHandler);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var receivingLink = Mock.Of<IReceivingAmqpLink>(l => l.Session == amqpSession && l.IsReceiver && l.Settings == new AmqpLinkSettings() && l.State == AmqpObjectState.Opened);

            var requestUri = new Uri("amqps://foo.bar/devices/d1");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = new AmqpMessageConverter();
            var body = new byte[] { 0, 1, 2, 3 };
            AmqpMessage message = AmqpMessage.Create(new Data { Value = new ArraySegment<byte>(body) });
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");

            // Act
            var receivingLinkHandler = new TestReceivingLinkHandler(identity, receivingLink, requestUri, boundVariables, connectionHandler, messageConverter);
            await receivingLinkHandler.OpenAsync(Amqp.Constants.DefaultTimeout);
            await receivingLinkHandler.ProcessMessageAsync(message);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.Equal(1, receivingLinkHandler.ReceivedMessages.Count);
            Assert.Equal(body, receivingLinkHandler.ReceivedMessages[0].GetPayloadBytes());
        }
    }

    class TestReceivingLinkHandler : ReceivingLinkHandler
    {
        public TestReceivingLinkHandler(IIdentity identity, IReceivingAmqpLink link,
            Uri requestUri, IDictionary<string, string> boundVariables, IConnectionHandler connectionHandler, IMessageConverter<AmqpMessage> messageConverter)
            : base(identity, link, requestUri, boundVariables, connectionHandler, messageConverter)
        {
        }

        public override LinkType Type => LinkType.Events;

        protected override QualityOfService QualityOfService => QualityOfService.AtLeastOnce;

        public IList<AmqpMessage> ReceivedMessages { get; } = new List<AmqpMessage>();

        protected override Task OnMessageReceived(AmqpMessage amqpMessage)
        {
            this.ReceivedMessages.Add(amqpMessage);
            return Task.CompletedTask;
        }
    }
}
