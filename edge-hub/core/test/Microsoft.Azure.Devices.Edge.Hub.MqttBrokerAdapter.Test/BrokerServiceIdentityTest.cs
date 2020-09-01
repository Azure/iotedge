// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Xunit;
    public class BrokerServiceIdentityTest
    {
        [Fact]
        public void SerializeTest()
        {
            string id = "id";
            string authChain = "authChain";
            BrokerServiceIdentity brokerServiceIdentity = new BrokerServiceIdentity(id, Option.Some(authChain));
            var s = JsonConvert.SerializeObject(brokerServiceIdentity);
            Assert.Equal($"{{\"Identity\":\"{id}\",\"AuthChain\":\"{authChain}\"}}", s);
        }

        [Fact]
        public void DeserializeTest()
        {
            string id = "id";
            string authChain = "authChain";
            string serializedString = $"{{\"Identity\":\"{id}\",\"AuthChain\":\"{authChain}\"}}";
            BrokerServiceIdentity brokerServiceIdentity = JsonConvert.DeserializeObject<BrokerServiceIdentity>(serializedString);
            Assert.Equal(brokerServiceIdentity.Identity, id);
            Assert.Equal(brokerServiceIdentity.AuthChain.OrDefault(), authChain);
        }

        [Fact]
        public void DeserializeTestNoAuthChain()
        {
            string id = "id";
            string serializedString = $"{{\"Identity\":\"{id}\"}}";
            BrokerServiceIdentity brokerServiceIdentity = JsonConvert.DeserializeObject<BrokerServiceIdentity>(serializedString);
            Assert.Equal(brokerServiceIdentity.Identity, id);
            Assert.False(brokerServiceIdentity.AuthChain.HasValue);
        }
    }
}
