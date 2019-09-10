// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Timer;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;
    using Serilog.Events;
    using static System.FormattableString;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Core.Constants;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using SystemProperties = Microsoft.Azure.Devices.Edge.Hub.Core.SystemProperties;

    public class RoutingEdgeHub : IEdgeHub
    {
        readonly Router router;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;
        readonly IConnectionManager connectionManager;
        readonly ITwinManager twinManager;
        readonly string edgeDeviceId;
        readonly IInvokeMethodHandler invokeMethodHandler;
        readonly ISubscriptionProcessor subscriptionProcessor;

        public RoutingEdgeHub(
            Router router,
            Core.IMessageConverter<IRoutingMessage> messageConverter,
            IConnectionManager connectionManager,
            ITwinManager twinManager,
            string edgeDeviceId,
            IInvokeMethodHandler invokeMethodHandler,
            ISubscriptionProcessor subscriptionProcessor)
        {
            this.router = Preconditions.CheckNotNull(router, nameof(router));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.twinManager = Preconditions.CheckNotNull(twinManager, nameof(twinManager));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.invokeMethodHandler = Preconditions.CheckNotNull(invokeMethodHandler, nameof(invokeMethodHandler));
            this.subscriptionProcessor = Preconditions.CheckNotNull(subscriptionProcessor, nameof(subscriptionProcessor));
        }

        public Task ProcessDeviceMessage(IIdentity identity, IMessage message)
        {
            Preconditions.CheckNotNull(message, nameof(message));
            Preconditions.CheckNotNull(identity, nameof(identity));
            Events.MessageReceived(identity, message);
            MetricsV0.MessageCount(identity, 1);
            using (MetricsV0.MessageLatency(identity))
            {
                IRoutingMessage routingMessage = this.ProcessMessageInternal(message, true);
                Metrics.AddMessageSize(routingMessage.Size(), identity.Id);
                return this.router.RouteAsync(routingMessage);
            }
        }

        public Task ProcessDeviceMessageBatch(IIdentity identity, IEnumerable<IMessage> messages)
        {
            IList<IMessage> messagesList = messages as IList<IMessage>
                                           ?? Preconditions.CheckNotNull(messages, nameof(messages)).ToList();
            Events.MessagesReceived(identity, messagesList);
            MetricsV0.MessageCount(identity, messagesList.Count);

            IEnumerable<IRoutingMessage> routingMessages = messagesList
                .Select(
                    m =>
                    {
                        IRoutingMessage routingMessage = this.ProcessMessageInternal(m, true);
                        Metrics.AddMessageSize(routingMessage.Size(), identity.Id);
                        return routingMessage;
                    });
            return this.router.RouteAsync(routingMessages);
        }

        public Task<DirectMethodResponse> InvokeMethodAsync(string id, DirectMethodRequest methodRequest)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Preconditions.CheckNotNull(methodRequest, nameof(methodRequest));

            Events.MethodCallReceived(id, methodRequest.Id, methodRequest.CorrelationId);
            return this.invokeMethodHandler.InvokeMethod(methodRequest);
        }

        public Task UpdateReportedPropertiesAsync(IIdentity identity, IMessage reportedPropertiesMessage)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(reportedPropertiesMessage, nameof(reportedPropertiesMessage));
            Events.UpdateReportedPropertiesReceived(identity);
            Task cloudSendMessageTask = this.twinManager.UpdateReportedPropertiesAsync(identity.Id, reportedPropertiesMessage);

            IRoutingMessage routingMessage = this.ProcessMessageInternal(reportedPropertiesMessage, false);
            Task routingSendMessageTask = this.router.RouteAsync(routingMessage);

            return Task.WhenAll(cloudSendMessageTask, routingSendMessageTask);
        }

        public Task SendC2DMessageAsync(string id, IMessage message)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Preconditions.CheckNotNull(message, nameof(message));

            Option<IDeviceProxy> deviceProxy = this.connectionManager.GetDeviceConnection(id);
            if (!deviceProxy.HasValue)
            {
                Events.UnableToSendC2DMessageNoDeviceConnection(id);
            }

            return deviceProxy.ForEachAsync(d => d.SendC2DMessageAsync(message));
        }

        public Task<IMessage> GetTwinAsync(string id)
        {
            Events.GetTwinCallReceived(id);
            return this.twinManager.GetTwinAsync(id);
        }

        public Task UpdateDesiredPropertiesAsync(string id, IMessage twinCollection)
        {
            Events.UpdateDesiredPropertiesCallReceived(id);
            return this.twinManager.UpdateDesiredPropertiesAsync(id, twinCollection);
        }

        public Task AddSubscription(string id, DeviceSubscription deviceSubscription)
            => this.subscriptionProcessor.AddSubscription(id, deviceSubscription);

        public Task RemoveSubscription(string id, DeviceSubscription deviceSubscription)
            => this.subscriptionProcessor.RemoveSubscription(id, deviceSubscription);

        public Task ProcessSubscriptions(string id, IEnumerable<(DeviceSubscription, bool)> subscriptions)
            => this.subscriptionProcessor.ProcessSubscriptions(id, subscriptions);

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.router?.Dispose();
            }
        }

        internal void AddEdgeSystemProperties(IMessage message)
        {
            message.SystemProperties[SystemProperties.EdgeMessageId] = Guid.NewGuid().ToString();
            if (message.SystemProperties.TryGetValue(SystemProperties.ConnectionDeviceId, out string deviceId))
            {
                string edgeHubOriginInterface = deviceId == this.edgeDeviceId
                    ? Constants.InternalOriginInterface
                    : Constants.DownstreamOriginInterface;
                message.SystemProperties[SystemProperties.EdgeHubOriginInterface] = edgeHubOriginInterface;
            }
        }

        static void ValidateMessageSize(IRoutingMessage messageToBeValidated)
        {
            long messageSize = messageToBeValidated.Size();
            if (messageSize > Constants.MaxMessageSize)
            {
                throw new EdgeHubMessageTooLargeException($"Message size is {messageSize} bytes which is greater than the max size {Constants.MaxMessageSize} bytes allowed");
            }
        }

        IRoutingMessage ProcessMessageInternal(IMessage message, bool validateSize)
        {
            this.AddEdgeSystemProperties(message);
            IRoutingMessage routingMessage = this.messageConverter.FromMessage(Preconditions.CheckNotNull(message, nameof(message)));

            // Validate message size
            if (validateSize)
            {
                ValidateMessageSize(routingMessage);
            }

            return routingMessage;
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.RoutingEdgeHub;
            static readonly ILogger Log = Logger.Factory.CreateLogger<RoutingEdgeHub>();

            enum EventIds
            {
                MethodReceived = IdStart,
                MessageReceived = 1501,
                ReportedPropertiesUpdateReceived = 1502,
                DesiredPropertiesUpdateReceived = 1503,
                DeviceConnectionNotFound
            }

            public static void MethodCallReceived(string fromId, string toId, string correlationId)
            {
                Log.LogDebug((int)EventIds.MethodReceived, Invariant($"Received method invoke call from {fromId} for {toId} with correlation ID {correlationId}"));
            }

            public static void UnableToSendC2DMessageNoDeviceConnection(string id)
            {
                Log.LogWarning((int)EventIds.DeviceConnectionNotFound, Invariant($"Unable to send C2D message to device {id} as an active device connection was not found."));
            }

            public static void MessagesReceived(IIdentity identity, IList<IMessage> messages)
            {
                if (Logger.GetLogLevel() <= LogEventLevel.Debug)
                {
                    string messageIdsString = messages
                        .Select(m => m.SystemProperties.TryGetValue(Devices.Routing.Core.SystemProperties.MessageId, out string messageId) ? messageId : string.Empty)
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .Join(", ");

                    if (!string.IsNullOrWhiteSpace(messageIdsString))
                    {
                        Log.LogDebug((int)EventIds.MessageReceived, Invariant($"Received {messages.Count} message(s) from {identity.Id} with message Id(s) [{messageIdsString}]"));
                    }
                    else
                    {
                        Log.LogDebug((int)EventIds.MessageReceived, Invariant($"Received {messages.Count} message(s) from {identity.Id}"));
                    }
                }
            }

            internal static void MessageReceived(IIdentity identity, IMessage message)
            {
                if (message.SystemProperties.TryGetValue(Devices.Routing.Core.SystemProperties.MessageId, out string messageId))
                {
                    Log.LogDebug((int)EventIds.MessageReceived, Invariant($"Received message from {identity.Id} with message Id {messageId}"));
                }
                else
                {
                    Log.LogDebug((int)EventIds.MessageReceived, Invariant($"Received message from {identity.Id}"));
                }
            }

            internal static void UpdateReportedPropertiesReceived(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.ReportedPropertiesUpdateReceived, Invariant($"Reported properties update message received from {identity.Id}"));
            }

            internal static void GetTwinCallReceived(string id)
            {
                Log.LogDebug((int)EventIds.MessageReceived, Invariant($"GetTwin call received from {id ?? string.Empty}"));
            }

            internal static void UpdateDesiredPropertiesCallReceived(string id)
            {
                Log.LogDebug((int)EventIds.DesiredPropertiesUpdateReceived, Invariant($"Desired properties update message received for {id ?? string.Empty}"));
            }
        }

        static class Metrics
        {
            static readonly IMetricsHistogram MessagesHistogram = Util.Metrics.Metrics.Instance.CreateHistogram(
                "message_size_bytes",
                "Size of messages received by EdgeHub",
                new List<string> { "id" });

            public static void AddMessageSize(long size, string id) => MessagesHistogram.Update(size, new[] { id });
        }

        static class MetricsV0
        {
            static readonly CounterOptions EdgeHubMessageReceivedCountOptions = new CounterOptions
            {
                Name = "EdgeHubMessageReceivedCount",
                MeasurementUnit = Unit.Events,
                ResetOnReporting = true,
            };

            static readonly TimerOptions EdgeHubMessageLatencyOptions = new TimerOptions
            {
                Name = "EdgeHubMessageLatencyMs",
                MeasurementUnit = Unit.None,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds
            };

            public static void MessageCount(IIdentity identity, long count) => Util.Metrics.MetricsV0.CountIncrement(GetTags(identity), EdgeHubMessageReceivedCountOptions, count);

            public static IDisposable MessageLatency(IIdentity identity) => Util.Metrics.MetricsV0.Latency(GetTags(identity), EdgeHubMessageLatencyOptions);

            static MetricTags GetTags(IIdentity identity)
            {
                return new MetricTags("Id", identity.Id);
            }
        }
    }
}
