// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using DotNetty.Codecs.Mqtt.Packets;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt.Subscription;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Xunit;

    public class TopicSubscriptionProviderTest
    {
        const string SomeTopicPrefix = "$iothub/messages/sometopic";
        const string MessagesTopicPrefix = "$iothub/messages/devicebound/#";
        const string MethodPostTopicPrefix = "$iothub/methods/POST/";
        const string TwinResTopicPrefix = "$iothub/twin/res/#";
        const string TwinPatchTopicPrefix = "$iothub/twin/PATCH/properties/desired/";
        const QualityOfService qos = QualityOfService.AtLeastOnce;

        static readonly ISubscription TwinRes = new TransientSubscription(TwinResTopicPrefix, qos);
        static readonly ISubscription MsgDeviceBound = new TransientSubscription(MessagesTopicPrefix, qos);
        static readonly ISubscription Method = new TransientSubscription(MethodPostTopicPrefix, qos);
        static readonly ISubscription SomeTopic = new TransientSubscription(SomeTopicPrefix, qos);
        static readonly ISubscription TwinDesired = new TransientSubscription(TwinPatchTopicPrefix, qos);

        [Fact]
        [Unit]
        public void TestGetAddSubscriptionRegistration_ShouldReturn_MethodRegistration()
        {
            ISubscriptionRegistration registration = SubscriptionProvider.GetAddSubscriptionRegistration(MethodPostTopicPrefix);

            Assert.NotNull(registration);
            Assert.IsType(typeof(MethodSubscriptionRegistration), registration);
        }

        [Fact]
        [Unit]
        public void TestGetRemoveSubscriptionRegistration_ShouldReturn_MethodDeregistration()
        {
            ISubscriptionRegistration registration = SubscriptionProvider.GetRemoveSubscriptionRegistration(MethodPostTopicPrefix);

            Assert.NotNull(registration);
            Assert.IsType(typeof(MethodSubscriptionDeregistration), registration);
        }

        [Fact]
        [Unit]
        public void TestGetAddSubscriptionRegistration_ShouldReturn_TwinRegistration()
        {
            ISubscriptionRegistration registration = SubscriptionProvider.GetAddSubscriptionRegistration(TwinPatchTopicPrefix);

            Assert.NotNull(registration);
            Assert.IsType(typeof(TwinSubscriptionRegistration), registration);
        }

        [Fact]
        [Unit]
        public void TestGetRemoveSubscriptionRegistration_ShouldReturn_TwinDeregistration()
        {
            ISubscriptionRegistration registration = SubscriptionProvider.GetRemoveSubscriptionRegistration(TwinPatchTopicPrefix);

            Assert.NotNull(registration);
            Assert.IsType(typeof(TwinSubscriptionDeregistration), registration);
        }

        [Fact]
        [Unit]
        public void TestGetAddSubscriptionRegistration_ShouldReturn_NoOpRegistration()
        {
            ISubscriptionRegistration registration = SubscriptionProvider.GetAddSubscriptionRegistration(SomeTopicPrefix);

            Assert.NotNull(registration);
            Assert.IsType(typeof(NullSubscriptionRegistration), registration);
        }

        [Fact]
        [Unit]
        public void TestGetRemoveSubscriptionRegistration_ShouldReturn_NoOpRegistration()
        {
            ISubscriptionRegistration registration = SubscriptionProvider.GetRemoveSubscriptionRegistration(SomeTopicPrefix);

            Assert.NotNull(registration);
            Assert.IsType(typeof(NullSubscriptionRegistration), registration);
        }
    }
}
