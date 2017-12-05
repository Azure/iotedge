// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;
    using IIdentity = Microsoft.Azure.Devices.Edge.Hub.Core.IIdentity;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;

    public class RoutingEdgeHub : IEdgeHub
    {
        readonly Router router;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;
        readonly IConnectionManager connectionManager;
        readonly ITwinManager twinManager;
        readonly string edgeDeviceId;
        const long MaxMessageSize = 256 * 1024; // matches IoTHub

        public RoutingEdgeHub(Router router, Core.IMessageConverter<IRoutingMessage> messageConverter,
            IConnectionManager connectionManager, ITwinManager twinManager, string edgeDeviceId)
        {
            this.router = Preconditions.CheckNotNull(router, nameof(router));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.twinManager = Preconditions.CheckNotNull(twinManager, nameof(twinManager));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
        }

        public Task ProcessDeviceMessage(IIdentity identity, IMessage message)
        {
            Preconditions.CheckNotNull(message, nameof(message));
            Preconditions.CheckNotNull(identity, nameof(identity));
            Events.MessageReceived(identity);
            IRoutingMessage routingMessage = this.ProcessMessageInternal(message, true);
            return this.router.RouteAsync(routingMessage);
        }

        public Task ProcessDeviceMessageBatch(IIdentity identity, IEnumerable<IMessage> messages)
        {
            IEnumerable<IRoutingMessage> routingMessages = Preconditions.CheckNotNull(messages)
                .Select(m => this.ProcessMessageInternal(m, true));
            return this.router.RouteAsync(routingMessages);
        }

        public Task<DirectMethodResponse> InvokeMethodAsync(IIdentity identity, DirectMethodRequest methodRequest)
        {
            Preconditions.CheckNotNull(methodRequest, nameof(methodRequest));

            Events.MethodCallReceived(identity, methodRequest.Id, methodRequest.CorrelationId);
            Option<IDeviceProxy> deviceProxy = this.connectionManager.GetDeviceConnection(methodRequest.Id);
            return deviceProxy.Match(
                dp => dp.InvokeMethodAsync(methodRequest),
                () => Task.FromResult(new DirectMethodResponse(null, null, (int)HttpStatusCode.NotFound)));
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

        private IRoutingMessage ProcessMessageInternal(IMessage message, bool validateSize)
        {
            this.AddEdgeSystemProperties(message);
            IRoutingMessage routingMessage = this.messageConverter.FromMessage(Preconditions.CheckNotNull(message, nameof(message)));

            // Validate message size
            long messageSize = routingMessage.Size();
            if (validateSize && messageSize > MaxMessageSize)
            {
                throw new InvalidOperationException($"Message size exceeds maximum allowed size: got {messageSize}, limit {MaxMessageSize}");
            }
            return routingMessage;
        }

        internal void AddEdgeSystemProperties(IMessage message)
        {
            message.SystemProperties[Core.SystemProperties.EdgeMessageId] = Guid.NewGuid().ToString();
            if (message.SystemProperties.TryGetValue(Core.SystemProperties.ConnectionDeviceId, out string deviceId))
            {
                string edgeHubOriginInterface = deviceId == this.edgeDeviceId
                    ? Core.Constants.InternalOriginInterface
                    : Core.Constants.DownstreamOriginInterface;
                message.SystemProperties[Core.SystemProperties.EdgeHubOriginInterface] = edgeHubOriginInterface;
            }
        }

        public Task<IMessage> GetTwinAsync(string id)
        {
            Events.GetTwinCallReceived(id);
            return this.twinManager.GetTwinAsync(id);
        }

        public Task UpdateDesiredPropertiesAsync(string id, IMessage twinCollection)
        {
            Events.UpdateDesiredPropertiesCallReceived(id);
            return   this.twinManager.UpdateDesiredPropertiesAsync(id, twinCollection);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.router?.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<RoutingEdgeHub>();
            const int IdStart = HubCoreEventIds.RoutingEdgeHub;

            enum EventIds
            {
                MethodReceived = IdStart,
                MessageReceived = 1501,
                ReportedPropertiesUpdateReceived = 1502,
                DesiredPropertiesUpdateReceived = 1503,
            }

            public static void MethodCallReceived(IIdentity identity, string id, string correlationId)
            {
                Log.LogDebug((int)EventIds.MethodReceived, Invariant($"Received method invoke call from {identity.Id} for {id} with correlation ID {correlationId}"));
            }

            internal static void MessageReceived(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.MessageReceived, Invariant($"Received message from {identity.Id}"));
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
    }
}
