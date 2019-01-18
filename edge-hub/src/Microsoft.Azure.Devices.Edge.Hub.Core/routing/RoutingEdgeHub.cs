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
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
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
        const long MaxMessageSize = 256 * 1024; // matches IoTHub
        readonly Router router;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;
        readonly IConnectionManager connectionManager;
        readonly ITwinManager twinManager;
        readonly string edgeDeviceId;
        readonly IInvokeMethodHandler invokeMethodHandler;

        public RoutingEdgeHub(
            Router router,
            Core.IMessageConverter<IRoutingMessage> messageConverter,
            IConnectionManager connectionManager,
            ITwinManager twinManager,
            string edgeDeviceId,
            IInvokeMethodHandler invokeMethodHandler,
            IDeviceConnectivityManager deviceConnectivityManager)
        {
            this.router = Preconditions.CheckNotNull(router, nameof(router));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.twinManager = Preconditions.CheckNotNull(twinManager, nameof(twinManager));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.invokeMethodHandler = Preconditions.CheckNotNull(invokeMethodHandler, nameof(invokeMethodHandler));
            deviceConnectivityManager.DeviceConnected += this.DeviceConnected;
        }

        public Task ProcessDeviceMessage(IIdentity identity, IMessage message)
        {
            Preconditions.CheckNotNull(message, nameof(message));
            Preconditions.CheckNotNull(identity, nameof(identity));
            Events.MessageReceived(identity, message);
            Metrics.MessageCount(identity, 1);
            using (Metrics.MessageLatency(identity))
            {
                IRoutingMessage routingMessage = this.ProcessMessageInternal(message, true);
                return this.router.RouteAsync(routingMessage);
            }
        }

        public Task ProcessDeviceMessageBatch(IIdentity identity, IEnumerable<IMessage> messages)
        {
            Preconditions.CheckNotNull(messages, nameof(messages));
            IList<IMessage> messagesList = messages as IList<IMessage> ?? messages.ToList();
            Events.MessagesReceived(identity, messagesList);
            Metrics.MessageCount(identity, messagesList.Count);

            IEnumerable<IRoutingMessage> routingMessages = messagesList
                .Select(m => this.ProcessMessageInternal(m, true));
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

        public async Task AddSubscription(string id, DeviceSubscription deviceSubscription)
        {
            Events.AddingSubscription(id, deviceSubscription);
            this.connectionManager.AddSubscription(id, deviceSubscription);
            try
            {
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                await this.ProcessSubscription(id, cloudProxy, deviceSubscription, true);
            }
            catch (Exception e)
            {
                Events.ErrorAddingSubscription(e, id, deviceSubscription);
            }
        }

        public async Task RemoveSubscription(string id, DeviceSubscription deviceSubscription)
        {
            Events.RemovingSubscription(id, deviceSubscription);
            this.connectionManager.RemoveSubscription(id, deviceSubscription);
            try
            {
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                await this.ProcessSubscription(id, cloudProxy, deviceSubscription, false);
            }
            catch (Exception e)
            {
                Events.ErrorRemovingSubscription(e, id, deviceSubscription);
            }
        }

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

        internal async Task ProcessSubscription(string id, Option<ICloudProxy> cloudProxy, DeviceSubscription deviceSubscription, bool addSubscription)
        {
            Events.ProcessingSubscription(id, deviceSubscription);
            switch (deviceSubscription)
            {
                case DeviceSubscription.C2D:
                    if (addSubscription)
                    {
                        cloudProxy.ForEach(c => c.StartListening());
                    }

                    break;

                case DeviceSubscription.DesiredPropertyUpdates:
                    await cloudProxy.ForEachAsync(c => addSubscription ? c.SetupDesiredPropertyUpdatesAsync() : c.RemoveDesiredPropertyUpdatesAsync());
                    break;

                case DeviceSubscription.Methods:
                    if (addSubscription)
                    {
                        await cloudProxy.ForEachAsync(c => c.SetupCallMethodAsync());
                        await this.invokeMethodHandler.ProcessInvokeMethodSubscription(id);
                    }
                    else
                    {
                        await cloudProxy.ForEachAsync(c => c.RemoveCallMethodAsync());
                    }

                    break;

                case DeviceSubscription.ModuleMessages:
                case DeviceSubscription.TwinResponse:
                case DeviceSubscription.Unknown:
                    // No Action required
                    break;
            }
        }

        static void ValidateMessageSize(IRoutingMessage messageToBeValidated)
        {
            long messageSize = messageToBeValidated.Size();
            if (messageSize > MaxMessageSize)
            {
                throw new EdgeHubMessageTooLargeException($"Message size is {messageSize} bytes which is greater than the max size {MaxMessageSize} bytes allowed");
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

        async void DeviceConnected(object sender, EventArgs eventArgs)
        {
            Events.DeviceConnectedProcessingSubscriptions();
            try
            {
                IEnumerable<IIdentity> connectedClients = this.connectionManager.GetConnectedClients().ToList();
                foreach (IIdentity identity in connectedClients)
                {
                    try
                    {
                        Events.ProcessingSubscriptions(identity);
                        await this.ProcessSubscriptions(identity.Id);
                    }
                    catch (Exception e)
                    {
                        Events.ErrorProcessingSubscriptions(e, identity);
                    }
                }
            }
            catch (Exception e)
            {
                Events.ErrorProcessingSubscriptions(e);
            }
        }

        async Task ProcessSubscriptions(string id)
        {
            Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
            Option<IReadOnlyDictionary<DeviceSubscription, bool>> subscriptions = this.connectionManager.GetSubscriptions(id);
            await subscriptions.ForEachAsync(
                async s =>
                {
                    foreach (KeyValuePair<DeviceSubscription, bool> subscription in s)
                    {
                        await this.ProcessSubscription(id, cloudProxy, subscription.Key, subscription.Value);
                    }
                });
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
                DeviceConnectionNotFound,
                ErrorProcessingSubscriptions,
                ErrorRemovingSubscription,
                ErrorAddingSubscription,
                AddingSubscription,
                RemovingSubscription,
                ProcessingSubscriptions,
                ProcessingSubscription
            }

            public static void MethodCallReceived(string fromId, string toId, string correlationId)
            {
                Log.LogDebug((int)EventIds.MethodReceived, Invariant($"Received method invoke call from {fromId} for {toId} with correlation ID {correlationId}"));
            }

            public static void UnableToSendC2DMessageNoDeviceConnection(string id)
            {
                Log.LogWarning((int)EventIds.DeviceConnectionNotFound, Invariant($"Unable to send C2D message to device {id} as an active device connection was not found."));
            }

            public static void ErrorProcessingSubscriptions(Exception ex, IIdentity identity)
            {
                if (ex.HasTimeoutException())
                {
                    Log.LogDebug((int)EventIds.ErrorProcessingSubscriptions, ex, Invariant($"Timed out while processing subscriptions for client {identity.Id}. Will try again when connected."));
                }
                else
                {
                    Log.LogWarning((int)EventIds.ErrorProcessingSubscriptions, ex, Invariant($"Error processing subscriptions for client {identity.Id}."));
                }
            }

            public static void ErrorRemovingSubscription(Exception ex, string id, DeviceSubscription subscription)
            {
                if (ex.HasTimeoutException())
                {
                    Log.LogDebug((int)EventIds.ErrorAddingSubscription, ex, Invariant($"Timed out while removing subscription {subscription} for client {id}. Will try again when connected."));
                }
                else
                {
                    Log.LogWarning((int)EventIds.ErrorRemovingSubscription, ex, Invariant($"Error removing subscription {subscription} for client {id}."));
                }
            }

            public static void ErrorAddingSubscription(Exception ex, string id, DeviceSubscription subscription)
            {
                if (ex.HasTimeoutException())
                {
                    Log.LogDebug((int)EventIds.ErrorAddingSubscription, ex, Invariant($"Timed out while adding subscription {subscription} for client {id}. Will try again when connected."));
                }
                else
                {
                    Log.LogDebug((int)EventIds.ErrorAddingSubscription, ex, Invariant($"Error adding subscription {subscription} for client {id}."));
                }
            }

            public static void AddingSubscription(string id, DeviceSubscription subscription)
            {
                Log.LogDebug((int)EventIds.AddingSubscription, Invariant($"Adding subscription {subscription} for client {id}."));
            }

            public static void RemovingSubscription(string id, DeviceSubscription subscription)
            {
                Log.LogDebug((int)EventIds.RemovingSubscription, Invariant($"Removing subscription {subscription} for client {id}."));
            }

            public static void ProcessingSubscriptions(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.ProcessingSubscriptions, Invariant($"Processing subscriptions for client {identity.Id}."));
            }

            public static void ProcessingSubscription(string id, DeviceSubscription deviceSubscription)
            {
                Log.LogInformation((int)EventIds.ProcessingSubscription, Invariant($"Processing subscription {deviceSubscription} for client {id}."));
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

            internal static void DeviceConnectedProcessingSubscriptions()
            {
                Log.LogInformation((int)EventIds.ProcessingSubscription, Invariant($"Device connected to cloud, processing subscriptions for connected clients."));
            }

            internal static void ErrorProcessingSubscriptions(Exception e)
            {
                Log.LogWarning((int)EventIds.ProcessingSubscription, e, Invariant($"Error processing subscriptions for connected clients."));
            }
        }

        static class Metrics
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

            public static void MessageCount(IIdentity identity, long count) => Util.Metrics.CountIncrement(GetTags(identity), EdgeHubMessageReceivedCountOptions, count);

            public static IDisposable MessageLatency(IIdentity identity) => Util.Metrics.Latency(GetTags(identity), EdgeHubMessageLatencyOptions);

            internal static MetricTags GetTags(IIdentity identity)
            {
                return new MetricTags("Id", identity.Id);
            }
        }
    }
}
