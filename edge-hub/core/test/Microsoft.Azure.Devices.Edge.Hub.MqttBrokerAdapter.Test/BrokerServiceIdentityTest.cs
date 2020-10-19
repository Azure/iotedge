// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
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

        [Fact]
        public void CompareTest()
        {
            string id1 = "id";
            string id2 = "id";
            string id3 = "id3";
            string authChain1 = "authChain";
            string authChain2 = "authChain";
            string authChain3 = "authChain3";
            BrokerServiceIdentity brokerServiceIdentity1 = new BrokerServiceIdentity(id1, Option.Some(authChain1));
            BrokerServiceIdentity brokerServiceIdentity2 = new BrokerServiceIdentity(id2, Option.Some(authChain2));
            BrokerServiceIdentity brokerServiceIdentity3 = new BrokerServiceIdentity(id3, Option.Some(authChain3));
            Assert.Equal(brokerServiceIdentity1, brokerServiceIdentity2);
            Assert.NotEqual(brokerServiceIdentity1, brokerServiceIdentity3);
        }
    }
}
