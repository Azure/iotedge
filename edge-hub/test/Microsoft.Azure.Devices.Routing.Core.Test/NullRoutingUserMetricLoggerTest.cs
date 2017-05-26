// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Xunit;

    [Unit]
    public class NullRoutingUserMetricLoggerTest
    {
        [Fact]
        public void LogEgressMetric()
        {
            var nullRoutingUserMetricLogger = NullRoutingUserMetricLogger.Instance;
            nullRoutingUserMetricLogger.LogEgressMetric(0, null, MessageRoutingStatus.Invalid, null);
        }

        [Fact]
        public void LogEgressFallbackMetric()
        {
            var nullRoutingUserMetricLogger = NullRoutingUserMetricLogger.Instance;
            nullRoutingUserMetricLogger.LogEgressFallbackMetric(0, null);
        }

        [Fact]
        public void LogEventHubEndpointEgressSuccessMetric()
        {
            var nullRoutingUserMetricLogger = NullRoutingUserMetricLogger.Instance;
            nullRoutingUserMetricLogger.LogEventHubEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogEventHubEndpointLatencyMetric()
        {
            var nullRoutingUserMetricLogger = NullRoutingUserMetricLogger.Instance;
            nullRoutingUserMetricLogger.LogEventHubEndpointLatencyMetric(0, null);
        }

        [Fact]
        public void LogQueueEndpointEgressSuccessMetric()
        {
            var nullRoutingUserMetricLogger = NullRoutingUserMetricLogger.Instance;
            nullRoutingUserMetricLogger.LogQueueEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogQueueEndpointLatencyMetric()
        {
            var nullRoutingUserMetricLogger = NullRoutingUserMetricLogger.Instance;
            nullRoutingUserMetricLogger.LogQueueEndpointLatencyMetric(0, null);
        }

        [Fact]
        public void LogTopicEndpointEgressSuccessMetric()
        {
            var nullRoutingUserMetricLogger = NullRoutingUserMetricLogger.Instance;
            nullRoutingUserMetricLogger.LogTopicEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogTopicEndpointLatencyMetric()
        {
            var nullRoutingUserMetricLogger = NullRoutingUserMetricLogger.Instance;
            nullRoutingUserMetricLogger.LogTopicEndpointLatencyMetric(0, null);
        }

        [Fact]
        public void LogBuiltInEndpointEgressSuccessMetric()
        {
            var nullRoutingUserMetricLogger = NullRoutingUserMetricLogger.Instance;
            nullRoutingUserMetricLogger.LogBuiltInEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogBuiltInEndpointLatencyMetric()
        {
            var nullRoutingUserMetricLogger = NullRoutingUserMetricLogger.Instance;
            nullRoutingUserMetricLogger.LogBuiltInEndpointLatencyMetric(0, null);
        }
    }
}
