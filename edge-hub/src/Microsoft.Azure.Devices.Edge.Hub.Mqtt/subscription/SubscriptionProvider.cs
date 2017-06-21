// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Subscription
{
    static class SubscriptionProvider
    {
        static readonly string MethodPostTopicPrefix = "$iothub/methods/POST/";
        static readonly string TwinPatchTopicPrefix = "$iothub/twin/PATCH/properties/desired/";
        static readonly string MessagesTopicSuffix = "messages/devicebound/#";

        public static ISubscriptionRegistration GetRemoveSubscriptionRegistration(string topicFilter)
        {
            if (topicFilter.StartsWith(MethodPostTopicPrefix))
            {
                return new MethodSubscriptionDeregistration();
            }
            else if (topicFilter.StartsWith(TwinPatchTopicPrefix))
            {
                return new TwinSubscriptionDeregistration();
            }
            return new NullSubscriptionRegistration();
        }

        public static ISubscriptionRegistration GetAddSubscriptionRegistration(string topicFilter)
        {
            if (topicFilter.StartsWith(MethodPostTopicPrefix))
            {
                return new MethodSubscriptionRegistration();
            }
            else if (topicFilter.StartsWith(TwinPatchTopicPrefix))
            {
                return new TwinSubscriptionRegistration();
            }
            else if (topicFilter.EndsWith(MessagesTopicSuffix))
            {
                return new MessagesSubscriptionRegistration();
            }
            return new NullSubscriptionRegistration();
        }
    }
}
