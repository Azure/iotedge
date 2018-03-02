// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class CbsLinkHandlerTest
    {
        [Fact]
        public void CreateTest()
        {
            // Arrange
            IAmqpLink registeredLink = null;
            var cbsNode = new Mock<ICbsNode>();
            cbsNode.Setup(c => c.RegisterLink(It.IsAny<IAmqpLink>())).Callback<IAmqpLink>(l => registeredLink = l);
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode.Object);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var amqpLink = Mock.Of<IAmqpLink>(l => l.Session == amqpSession && l.IsReceiver == false);

            var requestUri = new Uri("amqps://foo.bar/$cbs");
            var boundVariables = new Dictionary<string, string>();
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var connectionProvider = Mock.Of<IConnectionProvider>();

            // Act
            ILinkHandler linkHandler = CbsLinkHandler.Create(amqpLink, requestUri, boundVariables, messageConverter, connectionProvider);

            // Assert
            Assert.Equal(registeredLink, amqpLink);
            Assert.NotNull(linkHandler);
            Assert.IsType<CbsLinkHandler>(linkHandler);
        }
    }
}
