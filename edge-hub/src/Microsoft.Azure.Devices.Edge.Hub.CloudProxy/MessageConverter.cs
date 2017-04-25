
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class MessageConverter : IMessageConverter<Message>
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
            throw new System.NotImplementedException();
        }
    }
}
