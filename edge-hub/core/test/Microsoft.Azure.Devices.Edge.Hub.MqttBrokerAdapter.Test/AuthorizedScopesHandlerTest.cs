// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Moq;
    using System.Threading.Tasks;
    using Xunit;

    public class AuthorizedScopesHandlerTest
    {
        [Fact]
        public async Task PublishIdentitiesTest()
        {
            var capture = new SendCapture();
            var connector = GetConnector(capture);
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = new Mock(IDeviceScopeIdentitiesCache);

            var sut = new AuthorizedScopesHandler(connectionRegistry, identityProvider);

        }

        protected static IMqttBrokerConnector GetConnector(SendCapture sendCapture = null)
        {
            var connector = Mock.Of<IMqttBrokerConnector>();
            Mock.Get(connector)
                .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns((string topic, byte[] content) =>
                {
                    sendCapture?.Capture(topic, content);
                    return Task.FromResult(true);
                });

            return connector;
        }

        protected class SendCapture
        {
            public string Topic { get; private set; }
            public byte[] Content { get; private set; }

            public void Capture(string topic, byte[] content)
            {
                this.Topic = topic;
                this.Content = content;
            }
        }
    }
}
