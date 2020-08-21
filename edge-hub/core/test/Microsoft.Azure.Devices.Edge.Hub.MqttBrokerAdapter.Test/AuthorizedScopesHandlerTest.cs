// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Moq;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class AuthorizedScopesHandlerTest
    {
        [Fact]
        public void PublishIdentitiesTest()
        {
            // Arrange
            var capture = new SendCapture();
            var connector = GetConnector(capture);
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            var sut = new AuthorizedScopesHandler(deviceScopeIdentitiesCache.Object);
            sut.SetConnector(connector.Object);
            IList<string> identities = new List<string>() { "d1", "d2", "d3" };

            // Act
            deviceScopeIdentitiesCache.Raise(d => d.ServiceIdentitiesUpdated += null, null, identities);

            // Assert
            Assert.Equal("$edgehub/authorization", capture.Topic);
            Assert.Equal(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(identities)), capture.Content);
        }

        [Fact]
        public void GetAndPublishIdentitiesTest()
        {
            // Arrange
            var capture = new SendCapture();
            var connector = GetConnector(capture);
            IList<string> identities = new List<string>() { "d1", "d2", "d3" };
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            deviceScopeIdentitiesCache.Setup(d => d.GetAllIds()).ReturnsAsync(identities);
            var sut = new AuthorizedScopesHandler(deviceScopeIdentitiesCache.Object);
            sut.SetConnector(connector.Object);

            // Act
            connector.Raise(c => c.OnConnected += null, null, null);

            // Assert
            Assert.Equal("$edgehub/authorization", capture.Topic);
            Assert.Equal(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(identities)), capture.Content);
        }

        protected static Mock<IMqttBrokerConnector> GetConnector(SendCapture sendCapture = null)
        {
            var connector = new Mock<IMqttBrokerConnector>();
            connector
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
