// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    class MqttFeedbackMessage : IFeedbackMessage
    {
        readonly IMessage message;
        public MqttFeedbackMessage(IMessage message, FeedbackStatus status)
        {
            this.message = message;
            this.FeedbackStatus = status;
        }

        public FeedbackStatus FeedbackStatus { get; }

        public byte[] Body => this.message.Body;

        public IDictionary<string, string> Properties => this.message.Properties;

        public IDictionary<string, string> SystemProperties => this.message.SystemProperties;

        public void Dispose() => this.message.Dispose();
    }
}