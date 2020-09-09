// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt.Packets;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Xunit;

    public class SessionStateTest
    {
        const string MethodPostTopicPrefix = "$iothub/methods/POST/";
        const string TwinSubscriptionTopicPrefix = @"$iothub/twin/PATCH/properties/desired/";

        [Fact]
        [Unit]
        public void TestAddOrUpdateSubscription_ShouldAddSubscritionRegistration()
        {
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);

            IReadOnlyDictionary<string, bool> registrations = sessionState.SubscriptionRegistrations;
            IReadOnlyList<ISubscription> subs = sessionState.Subscriptions;

            Assert.NotNull(registrations);
            Assert.Equal(1, registrations.Count);
            Assert.True(registrations.ContainsKey(MethodPostTopicPrefix));
            Assert.True(registrations[MethodPostTopicPrefix]);
            Assert.NotNull(subs);
            Assert.Equal(1, subs.Count);
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

            IReadOnlyDictionary<string, bool> registrations = sessionState.SubscriptionRegistrations;
            IReadOnlyList<ISubscription> subs = sessionState.Subscriptions;

            Assert.NotNull(registrations);
            Assert.Equal(1, registrations.Count);
            Assert.True(registrations.ContainsKey(MethodPostTopicPrefix));
            Assert.False(registrations[MethodPostTopicPrefix]);
            Assert.NotNull(subs);
            Assert.Equal(0, subs.Count);
        }

        [Fact]
        [Unit]
        public void TestClearRegistrations_ShouldClearAllRegistrations()
        {
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            sessionState.AddOrUpdateSubscription("Sometopic", QualityOfService.AtLeastOnce);
            sessionState.ClearRegistrations();
            IReadOnlyDictionary<string, bool> registrations = sessionState.SubscriptionRegistrations;

            Assert.NotNull(registrations);
            Assert.Equal(0, registrations.Count);
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
            foreach (KeyValuePair<string, bool> subscriptionRegistration in copySession.SubscriptionRegistrations)
            {
                Assert.Equal(subscriptionRegistration.Value, sessionState.SubscriptionRegistrations[subscriptionRegistration.Key]);
            }
        }

        [Fact]
        [Unit]
        public async Task SessionStateSeralizationTest()
        {
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            sessionState.RemoveSubscription(TwinSubscriptionTopicPrefix);

            Assert.True(sessionState.SubscriptionRegistrations.ContainsKey(MethodPostTopicPrefix));
            Assert.True(sessionState.SubscriptionRegistrations.ContainsKey(TwinSubscriptionTopicPrefix));
            Assert.True(sessionState.SubscriptionRegistrations[MethodPostTopicPrefix]);
            Assert.False(sessionState.SubscriptionRegistrations[TwinSubscriptionTopicPrefix]);

            IEntityStore<string, SessionState> entityStore = new StoreProvider(new InMemoryDbStoreProvider()).GetEntityStore<string, SessionState>(Constants.SessionStorePartitionKey);
            string key = Guid.NewGuid().ToString();
            await entityStore.Put(key, sessionState);
            Option<SessionState> retrievedSessionStateOption = await entityStore.Get(key);
            Assert.True(retrievedSessionStateOption.HasValue);
            SessionState retrievedSessionState = retrievedSessionStateOption.OrDefault();
            Assert.NotNull(retrievedSessionState);
            Assert.NotNull(retrievedSessionState.Subscriptions);
            Assert.NotNull(retrievedSessionState.Subscriptions.FirstOrDefault(s => s.TopicFilter == MethodPostTopicPrefix));
            Assert.True(retrievedSessionState.SubscriptionRegistrations.ContainsKey(MethodPostTopicPrefix));
            Assert.True(retrievedSessionState.SubscriptionRegistrations.ContainsKey(TwinSubscriptionTopicPrefix));
            Assert.True(retrievedSessionState.SubscriptionRegistrations[MethodPostTopicPrefix]);
            Assert.False(retrievedSessionState.SubscriptionRegistrations[TwinSubscriptionTopicPrefix]);
        }
    }
}
