// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;

    public class SimpleRoutingMessageGeneratingStrategy : SimpleMessageGeneratingStrategyBase, IDeliverableGeneratingStrategy<Routing.Core.Message>
    {
        public static SimpleRoutingMessageGeneratingStrategy Create() => new SimpleRoutingMessageGeneratingStrategy();

        public new SimpleRoutingMessageGeneratingStrategy WithBodySize(int minBodySize, int maxBodySize)
        {
            base.WithBodySize(minBodySize, maxBodySize);
            return this;
        }

        public Routing.Core.Message Next()
        {
            var (body, properties, systemProperties) = this.GetComponents();
            var result = new Routing.Core.Message(TelemetryMessageSource.Instance, body, properties, systemProperties);

            return result;
        }
    }
}
