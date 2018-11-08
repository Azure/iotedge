// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;

    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
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

            // Act
            ILinkHandler linkHandler = CbsLinkHandler.Create(amqpLink, requestUri);

            // Assert
            Assert.Equal(registeredLink, amqpLink);
            Assert.NotNull(linkHandler);
            Assert.IsType<CbsLinkHandler>(linkHandler);
        }
    }
}
