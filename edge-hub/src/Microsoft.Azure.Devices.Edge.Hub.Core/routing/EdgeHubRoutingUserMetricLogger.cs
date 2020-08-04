// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;

    public class EdgeHubRoutingUserMetricLogger : IRoutingUserMetricLogger
    {
        readonly IMetricsCounter orphanedCounter;
        readonly IMetricsCounter unackCounter;

        EdgeHubRoutingUserMetricLogger()
        {
            this.orphanedCounter = Metrics.Instance.CreateCounter(
               "messages_dropped",
               "Messages dropped",
               new List<string> { "reason", "from", "from_route_output" });

            this.unackCounter = Metrics.Instance.CreateCounter(
               "messages_unack",
               "Messages not acknowledged",
               new List<string> { "reason", "from", "from_route_output"});
        }

        public static EdgeHubRoutingUserMetricLogger Instance { get; } = new EdgeHubRoutingUserMetricLogger();

        public void LogEgressMetric(long metricValue, string iotHubName, MessageRoutingStatus messageStatus, IMessage message)
        {
            switch (messageStatus)
            {
                case MessageRoutingStatus.Orphaned:
                    this.orphanedCounter.Increment(metricValue, new[] { "no_route", message.GetSenderId(), message.GetOutput() });
                    break;
            }
        }

        public void LogEgressFallbackMetric(long metricValue, string iotHubName)
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

        public void LogBuiltInEndpointEgressSuccessMetric(long metricValue, string iotHubName)
        {
        }

        public void LogBuiltInEndpointLatencyMetric(long metricValue, string iotHubName)
        {
        }

        public void LogIngressFailureMetric(long metricValue, string iothubName, IMessage message, string reason)
        {
            this.unackCounter.Increment(metricValue, new[] { reason, message.GetSenderId(), message.GetOutput() });
        }
    }
}
