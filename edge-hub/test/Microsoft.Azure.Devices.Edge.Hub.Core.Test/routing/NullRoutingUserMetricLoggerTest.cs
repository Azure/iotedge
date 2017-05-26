// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Xunit;

    [Unit]
    public class NullRoutingUserMetricLoggerTest
    {
        [Fact]
        public void LogEgressMetric()
        {
            var nullRoutingUserMetricLogger = new NullRoutingUserMetricLogger();
            nullRoutingUserMetricLogger.LogEgressMetric(0, null, MessageRoutingStatus.Invalid, null);
        }

        [Fact]
        public void LogEgressFallbackMetric()
        {
            var nullRoutingUserMetricLogger = new NullRoutingUserMetricLogger();
            nullRoutingUserMetricLogger.LogEgressFallbackMetric(0, null);
        }

        [Fact]
        public void LogEventHubEndpointEgressSuccessMetric()
        {
            var nullRoutingUserMetricLogger = new NullRoutingUserMetricLogger();
            nullRoutingUserMetricLogger.LogEventHubEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogEventHubEndpointLatencyMetric()
        {
            var nullRoutingUserMetricLogger = new NullRoutingUserMetricLogger();
            nullRoutingUserMetricLogger.LogEventHubEndpointLatencyMetric(0, null);
        }

        [Fact]
        public void LogQueueEndpointEgressSuccessMetric()
        {
            var nullRoutingUserMetricLogger = new NullRoutingUserMetricLogger();
            nullRoutingUserMetricLogger.LogQueueEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogQueueEndpointLatencyMetric()
        {
            var nullRoutingUserMetricLogger = new NullRoutingUserMetricLogger();
            nullRoutingUserMetricLogger.LogQueueEndpointLatencyMetric(0, null);
        }

        [Fact]
        public void LogTopicEndpointEgressSuccessMetric()
        {
            var nullRoutingUserMetricLogger = new NullRoutingUserMetricLogger();
            nullRoutingUserMetricLogger.LogTopicEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogTopicEndpointLatencyMetric()
        {
            var nullRoutingUserMetricLogger = new NullRoutingUserMetricLogger();
            nullRoutingUserMetricLogger.LogTopicEndpointLatencyMetric(0, null);
        }

        [Fact]
        public void LogBuiltInEndpointEgressSuccessMetric()
        {
            var nullRoutingUserMetricLogger = new NullRoutingUserMetricLogger();
            nullRoutingUserMetricLogger.LogBuiltInEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogBuiltInEndpointLatencyMetric()
        {
            var nullRoutingUserMetricLogger = new NullRoutingUserMetricLogger();
            nullRoutingUserMetricLogger.LogBuiltInEndpointLatencyMetric(0, null);
        }
    }
}
