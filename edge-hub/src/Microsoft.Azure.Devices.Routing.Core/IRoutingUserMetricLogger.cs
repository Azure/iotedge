// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    public interface IRoutingUserMetricLogger
    {
        void LogBuiltInEndpointEgressSuccessMetric(long metricValue, string iotHubName);

        void LogBuiltInEndpointLatencyMetric(long metricValue, string iotHubName);

        void LogEgressFallbackMetric(long metricValue, string iotHubName);

        void LogEgressMetric(long metricValue, string iotHubName, MessageRoutingStatus messageStatus, string messageSource);

        void LogEventHubEndpointEgressSuccessMetric(long metricValue, string iotHubName);

        void LogEventHubEndpointLatencyMetric(long metricValue, string iotHubName);

        void LogQueueEndpointEgressSuccessMetric(long metricValue, string iotHubName);

        void LogQueueEndpointLatencyMetric(long metricValue, string iotHubName);

        void LogTopicEndpointEgressSuccessMetric(long metricValue, string iotHubName);

        void LogTopicEndpointLatencyMetric(long metricValue, string iotHubName);
    }
}
