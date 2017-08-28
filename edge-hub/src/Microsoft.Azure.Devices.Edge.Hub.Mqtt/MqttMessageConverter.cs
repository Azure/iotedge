// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class MqttMessageConverter : IMessageConverter<Message>
    {
        public Message FromMessage(IMessage inputMessage)
        {
            Preconditions.CheckNotNull(inputMessage, nameof(inputMessage));
            Preconditions.CheckArgument(inputMessage.Body != null, "IMessage.Body should not be null");

            var message = new Message(inputMessage.Body);

            if (inputMessage.Properties != null)
            {
                foreach (KeyValuePair<string, string> inputMessageProperty in inputMessage.Properties)
                {
                    message.Properties.Add(inputMessageProperty);
                }
            }

            // TODO Check which SystemProperties need to be set.
            // Setting 3 System properties - MessageId, CorrelationId and UserId.
            if (inputMessage.SystemProperties != null)
            {
                if (inputMessage.SystemProperties.TryGetValue(SystemProperties.MessageId, out string messageId))
                {
                    message.MessageId = messageId;
                }

                if (inputMessage.SystemProperties.TryGetValue(SystemProperties.CorrelationId, out string correlationId))
                {
                    message.CorrelationId = correlationId;
                }

                if (inputMessage.SystemProperties.TryGetValue(SystemProperties.UserId, out string userId))
                {
                    message.UserId = userId;
                }
            }

            return message;
        }

        public IMessage ToMessage(Message sourceMessage)
        {
            MqttMessage message = new MqttMessage.Builder(sourceMessage.GetBytes())
                .SetProperties(sourceMessage.Properties)
                .Build();

            message.SystemProperties.Add(SystemProperties.MessageId, sourceMessage.MessageId);

            // TODO: Should uncomment code below instead of setting Edge time this code when session (which persist the subscriptions) is persisted in Edge. 
            // TODO: Without session persistence, messages that are sent when the device is not connected are rejected by Protocol Gateway
            // TODO: add clock skew time to EnqueuedTimeUtc: sourceMessage.EnqueuedTimeUtc.Add(ClockSkewAdjustment); IotHub value is 30 secs
            // DateTime createTime = sourceMessage.EnqueuedTimeUtc == DateTime.MinValue ? DateTime.UtcNow : sourceMessage.EnqueuedTimeUtc;
            DateTime createTime = DateTime.UtcNow;  


            message.SystemProperties.Add(SystemProperties.EnqueuedTime, createTime.ToString("o"));
            message.SystemProperties.Add(SystemProperties.LockToken, sourceMessage.LockToken);
            message.SystemProperties.Add(SystemProperties.DeliveryCount, sourceMessage.DeliveryCount.ToString());

            return message;
        }
    }
}
