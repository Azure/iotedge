// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Codecs.Mqtt.Packets;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Newtonsoft.Json;

    public class SessionState : ISessionState
    {
        readonly List<MqttSubscription> subscriptions;
        readonly Dictionary<string, bool> subscriptionRegistrations;

        // set transient to false to get calls from Protocol Gateway when there are changes to the subscription
        // because IsTransient is always set to false, this property is used to keep if the session was transient 
        // and it should not be saved to store in that case  
        public SessionState(bool transient)
            : this (false, !transient, new List<MqttSubscription>(), new Dictionary<string, bool>())
        {           
        }

        [JsonConstructor]
        public SessionState(bool isTransient, bool shouldSaveToStore, List<MqttSubscription> subscriptions, Dictionary<string, bool> subscriptionRegistrations)
        {
            this.IsTransient = isTransient;
            this.ShouldSaveToStore = shouldSaveToStore;
            this.subscriptions = subscriptions;
            this.subscriptionRegistrations = subscriptionRegistrations;
        }

        [JsonProperty(PropertyName = "isTransient")]
        public bool IsTransient { get; }

        [JsonProperty(PropertyName = "shouldSaveToStore")]
        public bool ShouldSaveToStore { get; }

        [JsonProperty(PropertyName = "subscriptions")]
        public IReadOnlyList<ISubscription> Subscriptions => this.subscriptions;

        [JsonProperty(PropertyName = "subscriptionRegistrations")]
        public IReadOnlyDictionary<string, bool> SubscriptionRegistrations => this.subscriptionRegistrations;

        // TODO: Check if this needs to be a deep copy.
        public ISessionState Copy() => new SessionState(
            this.IsTransient,
            this.ShouldSaveToStore,
            new List<MqttSubscription>(this.subscriptions),
            new Dictionary<string, bool>(this.subscriptionRegistrations));

        internal void ClearRegistrations() => this.subscriptionRegistrations.Clear();

        public bool RemoveSubscription(string topicFilter)
        {
            this.subscriptionRegistrations[topicFilter] = false;
            int index = this.FindSubscriptionIndex(topicFilter);
            if (index >= 0)
            {
                this.subscriptions.RemoveAt(index);
                return true;
            }
            return false;
        }

        public void AddOrUpdateSubscription(string topicFilter, QualityOfService qos)
        {
            this.subscriptionRegistrations[topicFilter] = true;
            int index = this.FindSubscriptionIndex(topicFilter);

            if (index >= 0)
            {
                this.subscriptions[index] = this.subscriptions[index].CreateUpdated(qos) as MqttSubscription;
            }
            else
            {
                this.subscriptions.Add(new MqttSubscription(topicFilter, qos));
            }
        }

        int FindSubscriptionIndex(string topicFilter)
        {
            for (int i = this.subscriptions.Count - 1; i >= 0; i--)
            {
                ISubscription subscription = this.subscriptions[i];
                if (subscription.TopicFilter.Equals(topicFilter, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
