// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Routing.Core;

    public class EdgeHubRoutingUserMetricLogger : IRoutingUserMetricLogger
    {
        public static EdgeHubRoutingUserMetricLogger Instance { get; } = new EdgeHubRoutingUserMetricLogger();

        public void LogEgressMetric(long metricValue, string iotHubName, MessageRoutingStatus messageStatus, IMessage message)
        {
            switch (messageStatus)
            {
                case MessageRoutingStatus.Orphaned:
                    Metrics.AddOrphanedMessage(metricValue, message.GetSenderId(), message.GetOutput());
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
            Metrics.AddUnackMessage(metricValue, message.GetSenderId(), message.GetOutput(), reason);
        }

        public void LogRetryOperation(long metricValue, string iothubName, string id, string type)
        {
            string operationName = "SendMessage";
            if (type == typeof(ModuleEndpoint).Name)
            {
                operationName = " SendMessageToModule";
            }
            else if (type == typeof(CloudEndpoint).Name)
            {
                operationName = " SendMessageToCloud";
            }

            Metrics.AddRetryOperation(metricValue, id, operationName);
        }

        static class Metrics
        {
            static readonly IMetricsCounter RetriesCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                    "operation_retry",
                    "Operation retries",
                    new List<string> { "id", "operation", MetricsConstants.MsTelemetry });

            static readonly IMetricsCounter OrphanedCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                   "messages_dropped",
                   "Messages dropped",
                   new List<string> { "reason", "from", "from_route_output", MetricsConstants.MsTelemetry });

            static readonly IMetricsCounter UnackCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                   "messages_unack",
                   "Messages not acknowledged",
                   new List<string> { "reason", "from", "from_route_output", MetricsConstants.MsTelemetry });

            public static void AddRetryOperation(long metricValue, string id, string operation) => RetriesCounter.Increment(metricValue, new[] { id, operation, bool.TrueString });

            public static void AddOrphanedMessage(long metricValue, string id, string output) => OrphanedCounter.Increment(metricValue, new[] { "no_route", id, output, bool.TrueString });

            public static void AddUnackMessage(long metricValue, string id, string output, string reason) => UnackCounter.Increment(metricValue, new[] { reason, id, output, bool.TrueString });
        }
    }
}
