// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
