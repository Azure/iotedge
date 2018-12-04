// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core
{
    public interface IRoutingUserMetricLogger
    {
        void LogEgressMetric(long metricValue, string iotHubName, MessageRoutingStatus messageStatus, string messageSource);

        void LogEgressFallbackMetric(long metricValue, string iotHubName);

        void LogEventHubEndpointEgressSuccessMetric(long metricValue, string iotHubName);

        void LogEventHubEndpointLatencyMetric(long metricValue, string iotHubName);

        void LogQueueEndpointEgressSuccessMetric(long metricValue, string iotHubName);

        void LogQueueEndpointLatencyMetric(long metricValue, string iotHubName);

        void LogTopicEndpointEgressSuccessMetric(long metricValue, string iotHubName);

        void LogTopicEndpointLatencyMetric(long metricValue, string iotHubName);

        void LogBuiltInEndpointEgressSuccessMetric(long metricValue, string iotHubName);

        void LogBuiltInEndpointLatencyMetric(long metricValue, string iotHubName);
    }
}
