// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    public class DeleteThisDummyProducer : IMqttSubscriber, IMqttMessageProducer, IMqttMessageConsumer
    {
        IMqttBridgeConnector connector;

        public IReadOnlyCollection<string> Subscriptions => new[] { "dummytopic" };

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            Console.WriteLine("received something {0} {1}", publishInfo.Topic, Encoding.ASCII.GetString(publishInfo.Payload));
            return Task.FromResult(true);
        }

        public void SetConnector(IMqttBridgeConnector connector)
        {
            this.connector = connector;

            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(5000);
                    await this.connector.SendAsync("dummytopic", Encoding.ASCII.GetBytes("repaszag"));
                }
            });
        }
    }
}
