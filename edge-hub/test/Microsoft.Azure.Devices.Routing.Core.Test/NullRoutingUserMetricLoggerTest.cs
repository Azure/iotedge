// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Xunit;

    [Unit]
    public class NullRoutingUserMetricLoggerTest
    {
        [Fact]
        public void LogBuiltInEndpointEgressSuccessMetric()
        {
            NullRoutingUserMetricLogger.Instance.LogBuiltInEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogBuiltInEndpointLatencyMetric()
        {
            NullRoutingUserMetricLogger.Instance.LogBuiltInEndpointLatencyMetric(0, null);
        }

        [Fact]
        public void LogEgressFallbackMetric()
        {
            NullRoutingUserMetricLogger.Instance.LogEgressFallbackMetric(0, null);
        }

        [Fact]
        public void LogEgressMetric()
        {
            NullRoutingUserMetricLogger.Instance.LogEgressMetric(0, null, MessageRoutingStatus.Invalid, null);
        }

        [Fact]
        public void LogEventHubEndpointEgressSuccessMetric()
        {
            NullRoutingUserMetricLogger.Instance.LogEventHubEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogEventHubEndpointLatencyMetric()
        {
            NullRoutingUserMetricLogger.Instance.LogEventHubEndpointLatencyMetric(0, null);
        }

        [Fact]
        public void LogQueueEndpointEgressSuccessMetric()
        {
            NullRoutingUserMetricLogger.Instance.LogQueueEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogQueueEndpointLatencyMetric()
        {
            NullRoutingUserMetricLogger.Instance.LogQueueEndpointLatencyMetric(0, null);
        }

        [Fact]
        public void LogTopicEndpointEgressSuccessMetric()
        {
            NullRoutingUserMetricLogger.Instance.LogTopicEndpointEgressSuccessMetric(0, null);
        }

        [Fact]
        public void LogTopicEndpointLatencyMetric()
        {
            NullRoutingUserMetricLogger.Instance.LogTopicEndpointLatencyMetric(0, null);
        }
    }
}
