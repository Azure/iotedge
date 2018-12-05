// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using Autofac;

    public abstract class RoutingUnitTestBase
    {
        static readonly IContainer AutofacContainer = RoutingTestModule.CreateContainer();

        protected RoutingUnitTestBase()
        {
            // Initialize call to performance counters
            AutofacContainer.Resolve<IRoutingPerfCounter>();
            AutofacContainer.Resolve<IRoutingUserMetricLogger>();
            AutofacContainer.Resolve<IRoutingUserAnalyticsLogger>();
        }
    }
}
