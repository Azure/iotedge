// Copyright (c) Microsoft. All rights reserved.
namespace Functions.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.EdgeHub;
    using Newtonsoft.Json;

    public static class EdgeHubSamples
    {
        [FunctionName("EdgeHubTrigger-CSharp")]
        public static async Task FilterMessageAndSendMessage(
            [EdgeHubTrigger("input1")] Message messageReceived,
            [EdgeHub(OutputName = "output1")] IAsyncCollector<Message> output)
        {
            const int defaultTemperatureThreshold = 19;
            byte[] messageBytes = messageReceived.GetBytes();
            var messageString = Encoding.UTF8.GetString(messageBytes);

            // Get message body, containing the Temperature data
            var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);

            if (messageBody != null && messageBody.Machine.Temperature > defaultTemperatureThreshold)
            {
                var filteredMessage = new Message(messageBytes);
                foreach (KeyValuePair<string, string> prop in messageReceived.Properties)
                {
                    filteredMessage.Properties.Add(prop.Key, prop.Value);
                }

                filteredMessage.Properties.Add("MessageType", "Alert");
                await output.AddAsync(filteredMessage);
            }
        }

        public class Ambient
        {
            public double Temperature { get; set; }

            public int Humidity { get; set; }
        }

        public class Machine
        {
            public double Temperature { get; set; }

            public double Pressure { get; set; }
        }

        public class MessageBody
        {
            public Machine Machine { get; set; }

            public Ambient Ambient { get; set; }

            public DateTime TimeCreated { get; set; }
        }
    }
}
