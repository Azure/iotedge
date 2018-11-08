// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.PerfCounters
{
    public class NullRoutingUserMetricLogger : IRoutingUserMetricLogger
    {
        public void LogBuiltInEndpointEgressSuccessMetric(long metricValue, string iotHubName)
        {
        }

        public void LogBuiltInEndpointLatencyMetric(long metricValue, string iotHubName)
        {
        }

        public void LogEgressFallbackMetric(long metricValue, string iotHubName)
        {
        }

        public void LogEgressMetric(long metricValue, string iotHubName, MessageRoutingStatus messageStatus, string messageSource)
        {
        }

        public void LogEventHubEndpointEgressSuccessMetric(long metricValue, string iotHubName)
        {
        }

        public void LogEventHubEndpointLatencyMetric(long metricValue, string iotHubName)
        {
        }

        public void LogQueueEndpointEgressSuccessMetric(long metricValue, string iotHubName)
        {
        }

        public void LogQueueEndpointLatencyMetric(long metricValue, string iotHubName)
        {
        }

        public void LogTopicEndpointEgressSuccessMetric(long metricValue, string iotHubName)
        {
        }

        public void LogTopicEndpointLatencyMetric(long metricValue, string iotHubName)
        {
        }
    }
}
