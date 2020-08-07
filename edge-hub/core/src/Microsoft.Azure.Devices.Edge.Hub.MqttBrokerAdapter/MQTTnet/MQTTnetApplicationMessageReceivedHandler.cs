// Copyright (c) Microsoft. All rights reserved.

using MQTTnet;
using MQTTnet.Client.Receiving;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.MQTTnet
{
    public class MQTTnetApplicationMessageReceivedHandler : IMqttApplicationMessageReceivedHandler
    {
        public Interfaces.IMessageHandler MessageHandler { set; get; }

        public async Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            var messageHandler = MessageHandler; 
            if(messageHandler == null)
            {
                eventArgs.ProcessingFailed = await messageHandler.ProcessMessageAsync(eventArgs.ApplicationMessage.Topic, eventArgs.ApplicationMessage.Payload);
            }
            else
            {
                eventArgs.ProcessingFailed = true;
            }
        }
    }
}
