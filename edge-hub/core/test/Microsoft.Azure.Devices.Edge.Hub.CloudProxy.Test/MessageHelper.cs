// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.EventHubs;
    using Newtonsoft.Json;

    public static class MessageHelper
    {
        /// <summary>
        /// Generates dummy messages for testing.
        /// </summary>
        public static IList<IMessage> GenerateMessages(int count)
        {
            var random = new Random();
            var messages = new List<IMessage>();
            for (int i = 0; i < count; i++)
            {
                var data = new
                {
                    temperature = random.Next(-20, 50),
                    scale = "Celsius"
                };
                string dataString = JsonConvert.SerializeObject(data);
                byte[] messageBytes = Encoding.UTF8.GetBytes(dataString);

                var properties = new Dictionary<string, string>()
                {
                    { "model", "temperature" },
                    { "level", "one" },
                    { "id", Guid.NewGuid().ToString() }
                };

                var systemProperties = new Dictionary<string, string>()
                {
                    { SystemProperties.MessageId, random.Next().ToString() }
                };

                var message = new EdgeMessage(messageBytes, properties, systemProperties);
                messages.Add(message);
            }

            return messages;
        }

        /// <summary>
        /// Checks if sent messages are a subset of messages received for each device.
        /// </summary>
        public static bool ValidateSentMessagesWereReceived(IDictionary<string, IList<IMessage>> sentMessagesByDevice, IDictionary<string, List<EventData>> receivedMessagesByPartition)
        {
            foreach (string deviceId in sentMessagesByDevice.Keys)
            {
                if (!receivedMessagesByPartition.ContainsKey(deviceId))
                {
                    return false;
                }

                foreach (IMessage message in sentMessagesByDevice[deviceId])
                {
                    EventData eventData = receivedMessagesByPartition[deviceId].FirstOrDefault(
                        m =>
                            m.Properties.ContainsKey("id") &&
                            m.Properties["id"] as string == message.Properties["id"]);
                    if (eventData == null || !message.Body.SequenceEqual(eventData.Body.Array))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
