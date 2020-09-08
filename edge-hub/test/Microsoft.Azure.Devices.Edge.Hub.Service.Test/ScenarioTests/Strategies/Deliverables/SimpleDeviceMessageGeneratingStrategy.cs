// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System.Threading;

    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public class SimpleDeviceMessageGeneratingStrategy : SimpleMessageGeneratingStrategyBase, IDeliverableGeneratingStrategy<IMessage>
    {
        public static SimpleDeviceMessageGeneratingStrategy Create() => new SimpleDeviceMessageGeneratingStrategy();

        public new SimpleDeviceMessageGeneratingStrategy WithBodySize(int minBodySize, int maxBodySize)
        {
            base.WithBodySize(minBodySize, maxBodySize);
            return this;
        }

        public IMessage Next()
        {
            var (body, properties, systemProperties) = this.GetComponents();
            var result = new EdgeMessage(body, properties, systemProperties);

            return result;
        }
    }
}
