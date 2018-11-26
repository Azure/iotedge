// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This class handles twin requests coming from the client (Get requests and
    /// reported properties updates)
    /// It handles receiving links that match the template /devices/{0}/twin or /devices/{0}/modules/{1}/twin
    /// </summary>
    public class TwinReceivingLinkHandler : ReceivingLinkHandler
    {
        public const string TwinPatch = "PATCH";
        public const string TwinGet = "GET";
        public const string TwinPut = "PUT";
        public const string TwinDelete = "DELETE";

        public TwinReceivingLinkHandler(IReceivingAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(link, requestUri, boundVariables, messageConverter)
        {
        }

        public override LinkType Type => LinkType.TwinReceiving;

        protected override QualityOfService QualityOfService => QualityOfService.AtMostOnce;

        public override string CorrelationId =>
            AmqpConnectionUtils.GetCorrelationId(this.Link);

        protected override async Task OnMessageReceived(AmqpMessage amqpMessage)
        {
            if (!amqpMessage.MessageAnnotations.Map.TryGetValue("operation", out string operation) ||
                string.IsNullOrWhiteSpace(operation))
            {
                Events.InvalidOperation(this);
            }

            string correlationId = amqpMessage.Properties.CorrelationId.ToString();

            switch (operation)
            {
                case TwinGet:
                    if (string.IsNullOrWhiteSpace(correlationId))
                    {
                        Events.InvalidCorrelationId(this);
                        return;
                    }

                    await this.DeviceListener.SendGetTwinRequest(correlationId);
                    Events.ProcessedTwinGetRequest(this);
                    break;

                case TwinPatch:
                    EdgeMessage reportedPropertiesMessage = new EdgeMessage.Builder(amqpMessage.GetPayloadBytes()).Build();
                    await this.DeviceListener.UpdateReportedPropertiesAsync(reportedPropertiesMessage, correlationId);
                    Events.ProcessedTwinReportedPropertiesUpdate(this);
                    break;

                case TwinPut:
                    if (string.IsNullOrWhiteSpace(correlationId))
                    {
                        Events.InvalidCorrelationId(this);
                        return;
                    }

                    await this.DeviceListener.AddDesiredPropertyUpdatesSubscription(correlationId);
                    Events.ProcessedDesiredPropertyUpdatesSubscriptionRequest(this, correlationId);
                    break;

                case TwinDelete:
                    if (string.IsNullOrWhiteSpace(correlationId))
                    {
                        Events.InvalidCorrelationId(this);
                        return;
                    }

                    await this.DeviceListener.RemoveDesiredPropertyUpdatesSubscription(correlationId);
                    Events.ProcessedDesiredPropertyUpdatesSubscriptionRemovalRequest(this, correlationId);
                    break;

                default:
                    Events.InvalidOperation(this, operation);
                    break;
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinReceivingLinkHandler>();
            const int IdStart = AmqpEventIds.TwinReceivingLinkHandler;

            enum EventIds
            {
                InvalidOperation = IdStart,
                ProcessedTwinGetRequest,
                ProcessedTwinReportedPropertiesUpdate,
                ProcessedDesiredPropertyUpdatesSubscriptionRequest,
                ProcessedDesiredPropertyUpdatesSubscriptionRemovalRequest
            }

            public static void InvalidOperation(TwinReceivingLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.InvalidOperation, $"Cannot process message on link {handler.LinkUri} because a valid operation was not specified in message annotations");
            }

            public static void ProcessedTwinGetRequest(TwinReceivingLinkHandler handler)
            {
                Log.LogDebug((int)EventIds.ProcessedTwinGetRequest, $"Processed Twin get request for {handler.ClientId}");
            }

            public static void ProcessedTwinReportedPropertiesUpdate(TwinReceivingLinkHandler handler)
            {
                Log.LogDebug((int)EventIds.ProcessedTwinReportedPropertiesUpdate, $"Processed Twin reported properties update for {handler.ClientId}");
            }

            public static void InvalidOperation(TwinReceivingLinkHandler handler, string operation)
            {
                Log.LogWarning((int)EventIds.InvalidOperation, $"Cannot process message on link {handler.LinkUri} because an invalid operation value '{operation}' was specified in message annotations");
            }

            public static void InvalidCorrelationId(TwinReceivingLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.InvalidOperation, $"Cannot process message on link {handler.LinkUri} because no correlation ID was specified");
            }

            public static void ProcessedDesiredPropertyUpdatesSubscriptionRequest(TwinReceivingLinkHandler handler, string correlationId)
            {
                Log.LogDebug((int)EventIds.ProcessedDesiredPropertyUpdatesSubscriptionRequest, $"Processed Twin desired properties subscription for {handler.ClientId} on request {correlationId}");
            }

            public static void ProcessedDesiredPropertyUpdatesSubscriptionRemovalRequest(TwinReceivingLinkHandler handler, string correlationId)
            {
                Log.LogDebug((int)EventIds.ProcessedDesiredPropertyUpdatesSubscriptionRemovalRequest, $"Processed removing Twin desired properties subscription for {handler.ClientId} on request {correlationId}");
            }
        }
    }
}
