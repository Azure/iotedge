// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Codecs.Mqtt.Packets;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt.Subscription;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    public class SessionState : ISessionState
    {
        readonly List<ISubscription> subscriptions;
        readonly List<ISubscriptionRegistration> subscriptionRegistrations;

        public SessionState(bool transient)
        {
            // set transient to false to get calls from Protocol Gateway when there are changes to the subscription
            this.IsTransient = false;
            // because IsTransient is always set to false, this property is used to keep if the session was transient 
            // and it should not be saved to store in that case  
            this.ShouldSaveToStore = !transient;
            this.subscriptions = new List<ISubscription>();
            this.subscriptionRegistrations = new List<ISubscriptionRegistration>();
        }

        internal IReadOnlyList<ISubscriptionRegistration> SubscriptionRegistrations => this.subscriptionRegistrations;

        internal void ClearRegistrations()
        {
            this.subscriptionRegistrations.Clear();
        }

        public bool IsTransient { get; }

        public bool ShouldSaveToStore { get; }

        public IReadOnlyList<ISubscription> Subscriptions => this.subscriptions;

        public ISessionState Copy()
        {
            var sessionState = new SessionState(!this.ShouldSaveToStore);
            sessionState.subscriptions.AddRange(this.Subscriptions);
            sessionState.subscriptionRegistrations.AddRange(this.SubscriptionRegistrations);
            return sessionState;
        }

        public bool RemoveSubscription(string topicFilter)
        {
            this.subscriptionRegistrations.Add(SubscriptionProvider.GetRemoveSubscriptionRegistration(topicFilter));
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
            this.subscriptionRegistrations.Add(SubscriptionProvider.GetAddSubscriptionRegistration(topicFilter));
            int index = this.FindSubscriptionIndex(topicFilter);

            if (index >= 0)
            {
                this.subscriptions[index] = this.subscriptions[index].CreateUpdated(qos);
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
