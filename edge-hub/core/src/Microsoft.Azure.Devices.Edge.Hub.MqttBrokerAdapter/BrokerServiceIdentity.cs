// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    /// <summary>
    /// BrokerServiceIdentity is a Data Transfer Object used for sending authorized identities with
    /// their AuthChains from EdgeHub core to Mqtt Broker.
    /// </summary>
    public class BrokerServiceIdentity : IComparable
    {
        public BrokerServiceIdentity(string identity, Option<string> authChain)
        {
            this.Identity = Preconditions.CheckNonWhiteSpace(identity, nameof(identity));
            this.AuthChain = authChain;
        }

        [JsonConstructor]
        public BrokerServiceIdentity(string identity, string authChain)
        {
            this.Identity = Preconditions.CheckNonWhiteSpace(identity, nameof(identity));
            this.AuthChain = Option.Maybe(authChain);
        }

        [JsonProperty("Identity")]
        public string Identity { get; }

        [JsonProperty("AuthChain")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> AuthChain { get; }

        public int CompareTo(object obj)
        {
            BrokerServiceIdentity brokerServiceIdentity = (BrokerServiceIdentity)obj;
            if (this.Identity.Equals(brokerServiceIdentity.Identity))
            {
                if (this.AuthChain.HasValue && brokerServiceIdentity.AuthChain.HasValue)
                {
                    if (this.AuthChain.OrDefault().Equals(brokerServiceIdentity.AuthChain.OrDefault()))
                    {
                        return 0;
                    }
                }
                else if (!this.AuthChain.HasValue && !brokerServiceIdentity.AuthChain.HasValue)
                {
                    return 0;
                }
            }

            return -1;
        }
    }
}
