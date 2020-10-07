// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    /// <summary>
    /// PolicyUpdate is a Data Transfer Object used for sending authorization policy
    /// definition from EdgeHub core to Mqtt Broker.
    /// </summary>
    internal class PolicyUpdate : IComparable
    {
        [JsonConstructor]
        public PolicyUpdate(string policy)
        {
            this.Definition = Preconditions.CheckNonWhiteSpace(policy, nameof(policy));
        }

        /// <summary>
        /// A string that contains new policy definition in json format.
        /// </summary>
        [JsonProperty]
        public string Definition { get; }

        public int CompareTo(object obj)
        {
            PolicyUpdate that = obj as PolicyUpdate;
            return this.Definition.CompareTo(that?.Definition);
        }
    }
}
