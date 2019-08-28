// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;

    public class RuntimeFixture : ManualProvisioningFixture
    {
        protected EdgeRuntime runtime;

        [SetUp]
        protected void GetRuntime()
        {
            this.runtime = new EdgeRuntime(
                Context.Current.DeviceId,
                Context.Current.EdgeAgentImage,
                Context.Current.EdgeHubImage,
                Context.Current.Proxy,
                Context.Current.Registries,
                Context.Current.OptimizeForPerformance,
                this.iotHub);
        }
    }
}
