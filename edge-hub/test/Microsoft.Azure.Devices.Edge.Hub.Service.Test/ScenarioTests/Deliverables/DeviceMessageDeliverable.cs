// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public class DeviceMessageDeliverable : StandardDeliverable<Core.IMessage, Client.Message, IDeviceListener>
    {
        private class DeliveryStrategy : IDeliveryStrategy<Core.IMessage, Client.Message, IDeviceListener>
        {
            public IDeliverableGeneratingStrategy<Core.IMessage> DefaultDeliverableGeneratingStrategy => new SimpleDeviceMessageGeneratingStrategy();

            public string GetDeliverableId(Core.IMessage message) => message.SystemProperties[Core.SystemProperties.MessageId];
            public string GetDeliverableId(Client.Message message) => message.MessageId;
            public Task SendDeliverable(IDeviceListener medium, Core.IMessage message) => medium.ProcessDeviceMessageAsync(message);
        }

        public static DeviceMessageDeliverable Create() => new DeviceMessageDeliverable();

        protected DeviceMessageDeliverable()
            : base(new DeliveryStrategy())
        {
        }
    }
}
