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
    using LinkHandlerMakerFunc = System.Func<
        IAmqpLink,
        System.Uri,
        System.Collections.Generic.IDictionary<string, string>,
        Core.IMessageConverter<Azure.Amqp.AmqpMessage>,
        Core.IConnectionProvider,
        LinkHandlers.ILinkHandler>;

    [Unit]
    public class LinkHandlerProviderTest
    {
        static IEnumerable<object[]> GetTestData()
        {
            yield return new object[] { "amqps://foo.bar/devices/device1", "/devices/{deviceId}", new Dictionary<string, string> { { "deviceId", "device1" } } };
            yield return new object[] { "amqps://foo.bar/devices/device1/modules/module1", "/devices/{deviceId}/modules/{moduleId}", new Dictionary<string, string> { { "deviceId", "device1" }, { "moduleId", "module1" } } };
            yield return new object[] { "amqps://foo.bar/p/q/r/s", "/{a}/{b}/{c}/{d}", new Dictionary<string, string> { { "a", "p" }, { "b", "q" }, { "c", "r" }, { "d", "s" } } };
            yield return new object[] { "amqps://foo.bar/a/b/c/d", "/a/b/c/d", new Dictionary<string, string>() };
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void CreateTest(string linkUri, string template, IDictionary<string, string> expectedBoundVariables)
        {
            // Arrange
            var amqpLink = Mock.Of<IAmqpLink>();
            var connectionProvider = Mock.Of<IConnectionProvider>();
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var linkHandler = Mock.Of<ILinkHandler>();
            bool makerCalled = false;
            ILinkHandler TestMaker(IAmqpLink link, Uri uri, IDictionary<string, string> boundVariables, IMessageConverter<AmqpMessage> converter, IConnectionProvider incomingConnectionProvider)
            {
                Assert.Equal(amqpLink, link);
                Assert.Equal(linkUri, uri.ToString());
                Assert.Equal(incomingConnectionProvider, connectionProvider);
                Assert.Equal(converter, messageConverter);
                makerCalled = true;
                return linkHandler;
            }

            var templates = new Dictionary<UriPathTemplate, LinkHandlerMakerFunc>
            {
                [new UriPathTemplate(template)] = TestMaker
            };

            var linkHandlerProvider = new LinkHandlerProvider(connectionProvider, messageConverter, templates);

            // Act
            ILinkHandler receivedLinkHandler = linkHandlerProvider.Create(amqpLink, new Uri(linkUri));

            // Assert
            Assert.True(makerCalled);
            Assert.Equal(linkHandler, receivedLinkHandler);
        }
    }
}
