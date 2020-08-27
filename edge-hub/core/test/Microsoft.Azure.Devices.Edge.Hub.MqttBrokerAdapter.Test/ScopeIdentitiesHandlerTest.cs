// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Moq;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ScopeIdentitiesHandlerTest
    {
        const string Topic = "$internal/identities";

        [Fact]
        public void PublishAddIdentitiesTest()
        {
            // Arrange
            var capture = new SendCapture();
            var connector = GetConnector(capture);
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            var sut = new ScopeIdentitiesHandler(deviceScopeIdentitiesCache.Object);
            sut.SetConnector(connector.Object);
            var serviceIdentity = new ServiceIdentity("d1", "genId", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("primKey", "secKey")), ServiceIdentityStatus.Enabled);
            var serviceIdentity2 = new ServiceIdentity("d2", "genId", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("primKey", "secKey")), ServiceIdentityStatus.Enabled);
            var serviceIdentity3 = new ServiceIdentity("d3", "genId", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("primKey", "secKey")), ServiceIdentityStatus.Enabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.IsAny<string>())).ReturnsAsync(Option.Some("testAuthChain"));
            deviceScopeIdentitiesCache.Setup(d => d.GetAllIds()).ReturnsAsync(new List<string>() { serviceIdentity.Id, serviceIdentity2.Id, serviceIdentity3.Id });
            BrokerServiceIdentity identity = new BrokerServiceIdentity("d1", Option.Some("testAuthChain"));
            BrokerServiceIdentity identity2 = new BrokerServiceIdentity("d2", Option.Some("testAuthChain"));
            BrokerServiceIdentity identity3 = new BrokerServiceIdentity("d3", Option.Some("testAuthChain"));
            connector.Raise(c => c.OnConnected += null, null, null);

            // Act
            deviceScopeIdentitiesCache.Raise(d => d.ServiceIdentitiesUpdated += null, null, new List<string> { serviceIdentity.Id, serviceIdentity2.Id, serviceIdentity3.Id });

            // Assert
            Assert.Equal(Topic, capture.Topic);
            Assert.Equal(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new List<BrokerServiceIdentity>() { identity, identity2, identity3 })), capture.Content);
        }

        [Fact]
        public void UpdateIdentitiesBeforeConnectTest()
        {
            // Arrange
            var capture = new SendCapture();
            var connector = GetConnector(capture);
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            var serviceIdentity = new ServiceIdentity("d1", "genId", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("primKey", "secKey")), ServiceIdentityStatus.Enabled);
            var serviceIdentity2 = new ServiceIdentity("d2", "genId", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("primKey", "secKey")), ServiceIdentityStatus.Enabled);
            var serviceIdentity3 = new ServiceIdentity("d3", "genId", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("primKey", "secKey")), ServiceIdentityStatus.Enabled);

            BrokerServiceIdentity identity = new BrokerServiceIdentity("d1", Option.Some("testAuthChain"));
            BrokerServiceIdentity identity2 = new BrokerServiceIdentity("d2", Option.Some("testAuthChain2"));

            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain("d1")).ReturnsAsync(Option.Some("testAuthChain"));
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain("d2")).ReturnsAsync(Option.Some("testAuthChain2"));
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain("d3")).ReturnsAsync(Option.Some("testAuthChain3"));

            var sut = new ScopeIdentitiesHandler(deviceScopeIdentitiesCache.Object);
            sut.SetConnector(connector.Object);

            // Act
            deviceScopeIdentitiesCache.Raise(d => d.ServiceIdentitiesUpdated += null, null, new List<string>() { serviceIdentity.Id, serviceIdentity2.Id, serviceIdentity3.Id });
            deviceScopeIdentitiesCache.Raise(d => d.ServiceIdentitiesUpdated += null, null, new List<string>() { serviceIdentity.Id, serviceIdentity2.Id });


            // Assert
            Assert.Null(capture.Topic);
            Assert.Null(capture.Content);

            // Act
            connector.Raise(c => c.OnConnected += null, null, null);

            // Assert
            Assert.Equal(Topic, capture.Topic);
            IList<BrokerServiceIdentity> brokerServiceIdentities = JsonConvert.DeserializeObject<IList<BrokerServiceIdentity>>(Encoding.UTF8.GetString(capture.Content));
            Assert.Equal(2, brokerServiceIdentities.Count);
            Assert.Contains(identity, brokerServiceIdentities);
            Assert.Contains(identity2, brokerServiceIdentities);
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
