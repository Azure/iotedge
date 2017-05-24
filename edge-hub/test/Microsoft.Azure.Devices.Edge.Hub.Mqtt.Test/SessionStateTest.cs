// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System.Collections.Generic;
    using DotNetty.Codecs.Mqtt.Packets;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt.Subscription;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Xunit;

    public class SessionStateTest
    {
        const string MethodPostTopicPrefix = "$iothub/methods/POST/";

        [Fact]
        [Unit]
        public void TestAddOrUpdateSubscription_ShouldAddSubscritionRegistration()
        {
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);

            IReadOnlyList<ISubscriptionRegistration> registrations = sessionState.SubscriptionRegistrations;
            IReadOnlyList<ISubscription> subs = sessionState.Subscriptions;

            Assert.NotNull(registrations);
            Assert.Equal(registrations.Count, 1);
            Assert.IsType(typeof(MethodSubscriptionRegistration), registrations[0]);
            Assert.NotNull(subs);
            Assert.Equal(subs.Count, 1);
            Assert.Equal(subs[0].TopicFilter, MethodPostTopicPrefix);
        }

        [Fact]
        [Unit]
        public void TestRemoveSubscription_ShouldAddSubscritionDeregistration()
        {
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            sessionState.ClearRegistrations();

            sessionState.RemoveSubscription(MethodPostTopicPrefix);

            IReadOnlyList<ISubscriptionRegistration> registrations = sessionState.SubscriptionRegistrations;
            IReadOnlyList<ISubscription> subs = sessionState.Subscriptions;

            Assert.NotNull(registrations);
            Assert.Equal(registrations.Count, 1);
            Assert.IsType(typeof(MethodSubscriptionDeregistration), registrations[0]);
            Assert.NotNull(subs);
            Assert.Equal(subs.Count, 0);
        }

        [Fact]
        [Unit]
        public void TestClearRegistrations_ShouldClearAllRegistrations()
        {
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            sessionState.AddOrUpdateSubscription("Sometopic", QualityOfService.AtLeastOnce);
            sessionState.ClearRegistrations();
            IReadOnlyList<ISubscriptionRegistration> registrations = sessionState.SubscriptionRegistrations;

            Assert.NotNull(registrations);
            Assert.Equal(registrations.Count, 0);
        }

        [Fact]
        [Unit]
        public void TestCopy_ShouldReturnSessionStateCopy()
        {
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            sessionState.AddOrUpdateSubscription("Sometopic", QualityOfService.AtLeastOnce);

            var copySession = sessionState.Copy() as SessionState;

            Assert.NotNull(copySession);
            Assert.Equal(copySession.Subscriptions.Count, sessionState.Subscriptions.Count);
            for (int i = 0; i < copySession.Subscriptions.Count; i++)
            {
                Assert.Equal(copySession.Subscriptions[i].TopicFilter, sessionState.Subscriptions[i].TopicFilter);
                Assert.Equal(copySession.Subscriptions[i].QualityOfService, sessionState.Subscriptions[i].QualityOfService);
                Assert.Equal(copySession.Subscriptions[i].CreationTime, sessionState.Subscriptions[i].CreationTime);
            }

            Assert.Equal(copySession.SubscriptionRegistrations.Count, sessionState.SubscriptionRegistrations.Count);
            for (int i = 0; i < copySession.SubscriptionRegistrations.Count; i++)
            {
                Assert.Equal(copySession.SubscriptionRegistrations[i].GetType(), sessionState.SubscriptionRegistrations[i].GetType());
            }
        }
    }
}
