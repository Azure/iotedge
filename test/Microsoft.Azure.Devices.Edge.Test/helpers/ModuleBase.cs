// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;

    public class ModuleBase : TestBase
    {
        protected IotHub iotHub;
        protected EdgeRuntime runtime;

        [SetUp]
        protected void BeforeEachModuleTest()
        {
            string agentImage = Context.Current.EdgeAgentImage.Expect(() => new ArgumentException());
            string hubImage = Context.Current.EdgeHubImage.Expect(() => new ArgumentException());

            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.Proxy);

            this.runtime = new EdgeRuntime(
                Context.Current.DeviceId,
                agentImage,
                hubImage,
                Context.Current.Proxy,
                Context.Current.Registries,
                Context.Current.OptimizeForPerformance,
                this.iotHub);
        }
    }
}
