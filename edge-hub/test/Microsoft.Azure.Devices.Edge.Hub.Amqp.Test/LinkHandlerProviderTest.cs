// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class LinkHandlerProviderTest
    {
        public static IEnumerable<object[]> GetLinkTypeTestData()
        {
            yield return new object[] { "amqps://foo.bar/$cbs", true, LinkType.Cbs, new Dictionary<string, string>() };
            yield return new object[] { "amqps://foo.bar/$cbs", false, LinkType.Cbs, new Dictionary<string, string>() };
            yield return new object[] { "amqps://foo.bar//devices/device1/messages/events", true, LinkType.Events, new Dictionary<string, string> { { "deviceid", "device1" } } };
            yield return new object[]
            {
                "amqps://foo.bar/devices/device1/modules/module1/messages/events", true, LinkType.Events, new Dictionary<string, string>
                {
                    { "deviceid", "device1" },
                    { "moduleid", "module1" }
                }
            };
            yield return new object[]
            {
                "amqps://foo.bar/devices/device1/modules/module1/messages/events", false, LinkType.ModuleMessages, new Dictionary<string, string>
                {
                    { "deviceid", "device1" },
                    { "moduleid", "module1" }
                }
            };
            yield return new object[] { "amqps://foo.bar/devices/device1/messages/deviceBound", false, LinkType.C2D, new Dictionary<string, string> { { "deviceid", "device1" } } };
            yield return new object[] { "amqps://foo.bar/devices/device1/methods/deviceBound", false, LinkType.MethodSending, new Dictionary<string, string> { { "deviceid", "device1" } } };
            yield return new object[]
            {
                "amqps://foo.bar/devices/device1/modules/module1/methods/deviceBound", false, LinkType.MethodSending, new Dictionary<string, string>
                {
                    { "deviceid", "device1" },
                    { "moduleid", "module1" }
                }
            };
            yield return new object[] { "amqps://foo.bar/devices/device1/methods/deviceBound", true, LinkType.MethodReceiving, new Dictionary<string, string> { { "deviceid", "device1" } } };
            yield return new object[]
            {
                "amqps://foo.bar/devices/device1/modules/module1/methods/deviceBound", true, LinkType.MethodReceiving, new Dictionary<string, string>
                {
                    { "deviceid", "device1" },
                    { "moduleid", "module1" }
                }
            };
            yield return new object[] { "amqps://foo.bar/devices/device1/twin", false, LinkType.TwinSending, new Dictionary<string, string> { { "deviceid", "device1" } } };
            yield return new object[]
            {
                "amqps://foo.bar/devices/device1/modules/module1/twin", false, LinkType.TwinSending, new Dictionary<string, string>
                {
                    { "deviceid", "device1" },
                    { "moduleid", "module1" }
                }
            };
            yield return new object[] { "amqps://foo.bar/devices/device1/twin", true, LinkType.TwinReceiving, new Dictionary<string, string> { { "deviceid", "device1" } } };
            yield return new object[]
            {
                "amqps://foo.bar/devices/device1/modules/module1/twin", true, LinkType.TwinReceiving, new Dictionary<string, string>
                {
                    { "deviceid", "device1" },
                    { "moduleid", "module1" }
                }
            };
        }

        public static IEnumerable<object[]> GetInvalidLinkTypeTestData()
        {
            yield return new object[] { "amqps://foo.bar/$cbs2", true };
            yield return new object[] { "amqps://foo.bar/cbs", false };
            yield return new object[] { "amqps://foo.bar//devices/device1/messages/events", false };
            yield return new object[] { "amqps://foo.bar/devices/device1/modules/module1/messages", true };
            yield return new object[] { "amqps://foo.bar/devices/device1/modules/module1/messages/foo", false };
            yield return new object[] { "amqps://foo.bar/devices/device1/messages/deviceBound", true };
            yield return new object[] { "amqps://foo.bar/devices/device1/twin2", false };
            yield return new object[] { "amqps://foo.bar/devices/device1/modules/module1/twin/method", true };
        }

        public static IEnumerable<object[]> GetLinkHandlerTestData()
        {
            yield return new object[] { "amqps://foo.bar/$cbs", true, typeof(CbsLinkHandler) };
            yield return new object[] { "amqps://foo.bar/$cbs", false, typeof(CbsLinkHandler) };
            yield return new object[] { "amqps://foo.bar//devices/device1/messages/events", true, typeof(EventsLinkHandler) };
            yield return new object[] { "amqps://foo.bar/devices/device1/modules/module1/messages/events", true, typeof(EventsLinkHandler) };
            yield return new object[] { "amqps://foo.bar/devices/device1/modules/module1/messages/events", false, typeof(ModuleMessageLinkHandler) };
            yield return new object[] { "amqps://foo.bar/devices/device1/messages/deviceBound", false, typeof(DeviceBoundLinkHandler) };
            yield return new object[] { "amqps://foo.bar/devices/device1/methods/deviceBound", false, typeof(MethodSendingLinkHandler) };
            yield return new object[] { "amqps://foo.bar/devices/device1/modules/module1/methods/deviceBound", false, typeof(MethodSendingLinkHandler) };
            yield return new object[] { "amqps://foo.bar/devices/device1/methods/deviceBound", true, typeof(MethodReceivingLinkHandler) };
            yield return new object[] { "amqps://foo.bar/devices/device1/modules/module1/methods/deviceBound", true, typeof(MethodReceivingLinkHandler) };
            yield return new object[] { "amqps://foo.bar/devices/device1/twin", false, typeof(TwinSendingLinkHandler) };
            yield return new object[] { "amqps://foo.bar/devices/device1/modules/module1/twin", false, typeof(TwinSendingLinkHandler) };
            yield return new object[] { "amqps://foo.bar/devices/device1/twin", true, typeof(TwinReceivingLinkHandler) };
            yield return new object[] { "amqps://foo.bar/devices/device1/modules/module1/twin", true, typeof(TwinReceivingLinkHandler) };
        }

        public static IEnumerable<object[]> GetIdentityFromBoundVariablesTestData()
        {
            yield return new object[]
            {
                new Dictionary<string, string>
                {
                    ["deviceid"] = "d1"
                },
                "d1",
                string.Empty
            };

            yield return new object[]
            {
                new Dictionary<string, string>
                {
                    ["deviceid"] = "d1",
                    ["moduleid"] = "m1"
                },
                "d1",
                "m1"
            };

            yield return new object[]
            {
                new Dictionary<string, string>
                {
                    ["deviceid"] = "d%40n.f",
                    ["moduleid"] = "m%40n.p"
                },
                "d@n.f",
                "m@n.p"
            };
        }

        [Theory]
        [MemberData(nameof(GetLinkTypeTestData))]
        public void GetLinkTypeTest(string linkUri, bool isReceiver, LinkType expectedLinkType, IDictionary<string, string> expectedBoundVariables)
        {
            // Arrange
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var twinMessageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var methodMessageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var identityProvider = new IdentityProvider("foo.bar");
            var linkHandlerProvider = new LinkHandlerProvider(messageConverter, twinMessageConverter, methodMessageConverter, identityProvider);

            var amqpLink = Mock.Of<IAmqpLink>(l => l.IsReceiver == isReceiver);
            var uri = new Uri(linkUri);

            // Act
            (LinkType LinkType, IDictionary<string, string> BoundVariables) match = linkHandlerProvider.GetLinkType(amqpLink, uri);

            // Assert
            Assert.Equal(expectedLinkType, match.LinkType);
            Assert.Equal(expectedBoundVariables, match.BoundVariables);
        }

        [Theory]
        [MemberData(nameof(GetInvalidLinkTypeTestData))]
        public void GetInvalidLinkTypeTest(string linkUri, bool isReceiver)
        {
            // Arrange
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var twinMessageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var methodMessageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var identityProvider = new IdentityProvider("foo.bar");
            var linkHandlerProvider = new LinkHandlerProvider(messageConverter, twinMessageConverter, methodMessageConverter, identityProvider);

            var amqpLink = Mock.Of<IAmqpLink>(l => l.IsReceiver == isReceiver);
            var uri = new Uri(linkUri);

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => linkHandlerProvider.GetLinkType(amqpLink, uri));
        }

        [Theory]
        [MemberData(nameof(GetLinkHandlerTestData))]
        public void GetLinkHandlerTest(string url, bool isReceiver, Type expectedLinkHandlerType)
        {
            // Arrange
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var twinMessageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var methodMessageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var identityProvider = new IdentityProvider("foo.bar");
            var linkHandlerProvider = new LinkHandlerProvider(messageConverter, twinMessageConverter, methodMessageConverter, identityProvider);

            var uri = new Uri(url);
            var amqpClientConnectionsHandler = Mock.Of<IClientConnectionsHandler>(c => c.GetConnectionHandler(It.IsAny<IIdentity>()) == Mock.Of<IConnectionHandler>());
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<IClientConnectionsHandler>() == amqpClientConnectionsHandler);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            IAmqpLink amqpLink = isReceiver
                ? Mock.Of<IReceivingAmqpLink>(l => l.IsReceiver && l.Session == amqpSession)
                : Mock.Of<ISendingAmqpLink>(l => !l.IsReceiver && l.Session == amqpSession) as IAmqpLink;
            if (url.Contains("$cbs"))
            {
                Mock.Get(amqpConnection).Setup(c => c.FindExtension<ICbsNode>()).Returns(Mock.Of<ICbsNode>());
            }

            // Act
            ILinkHandler linkHandler = linkHandlerProvider.Create(amqpLink, uri);

            // Assert
            Assert.NotNull(linkHandler);
            Assert.IsType(expectedLinkHandlerType, linkHandler);
        }

        [Theory]
        [MemberData(nameof(GetIdentityFromBoundVariablesTestData))]
        public void GetIdentityFromBoundVariablesTest(IDictionary<string, string> boundVariables, string expectedDeviceId, string expectedModuleId)
        {
            // Arrange
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var twinMessageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var methodMessageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var identityProvider = new IdentityProvider("foo.azure-device.net");
            var linkHandlerProvider = new LinkHandlerProvider(messageConverter, twinMessageConverter, methodMessageConverter, identityProvider);

            // Act
            IIdentity identity = linkHandlerProvider.GetIdentity(boundVariables);

            // Assert
            if (string.IsNullOrEmpty(expectedModuleId))
            {
                Assert.IsType<DeviceIdentity>(identity);
                Assert.Equal(expectedDeviceId, identity.Id);
            }
            else
            {
                Assert.IsType<ModuleIdentity>(identity);
                Assert.Equal(expectedDeviceId, ((ModuleIdentity)identity).DeviceId);
                Assert.Equal(expectedModuleId, ((ModuleIdentity)identity).ModuleId);
            }
        }
    }
}
