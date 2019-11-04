// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Shared;

    public class TwinCollectionDeliverable : StandardDeliverable<TwinCollection, Core.IMessage, Func<TwinCollection, Task>>
    {
        private class DeliveryStrategy : IDeliveryStrategy<TwinCollection, Core.IMessage, Func<TwinCollection, Task>>
        {
            private TwinCollectionMessageConverter messageConverter = new TwinCollectionMessageConverter();

            public IDeliverableGeneratingStrategy<TwinCollection> DefaultDeliverableGeneratingStrategy => new SimpleTwinCollectionGeneratingStrategy();

            public string GetDeliverableId(TwinCollection deliverable) => deliverable["counter"].ToString();
            public string GetDeliverableId(Core.IMessage deliverable) => this.messageConverter.FromMessage(deliverable)["counter"].ToString();
            public Task SendDeliverable(Func<TwinCollection, Task> media, TwinCollection deliverable) => media(deliverable);
        }

        public static TwinCollectionDeliverable Create() => new TwinCollectionDeliverable();

        protected TwinCollectionDeliverable()
            : base(new DeliveryStrategy())
        {
        }
    }
}
