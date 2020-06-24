// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Routing.Core;
    using HubMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;

    class MqttFeedbackMessage : IFeedbackMessage
    {
        readonly HubMessage message;

        public MqttFeedbackMessage(HubMessage message, FeedbackStatus status)
        {
            this.message = message;
            this.FeedbackStatus = status;
        }

        public FeedbackStatus FeedbackStatus { get; }

        public byte[] Body => this.message.Body;

        public IDictionary<string, string> Properties => this.message.Properties;

        public IDictionary<string, string> SystemProperties => this.message.SystemProperties;

        public uint ProcessedPriority => RouteFactory.DefaultPriority;

        public void Dispose() => this.message.Dispose();
    }
}
