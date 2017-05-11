// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using Autofac;
    using Microsoft.Azure.Devices.Routing.Core.Test.PerfCounters;

    public sealed class RoutingTestModule : Module
    {
        RoutingTestModule()
        {
        }

        public static IContainer CreateContainer()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new RoutingTestModule());
            return builder.Build();
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IRoutingPerfCounter
            builder.Register(c =>
            {
                Routing.PerfCounter = new NullRoutingPerfCounter();
                return Routing.PerfCounter;
            })
                .As<IRoutingPerfCounter>()
                .SingleInstance();

            // IRoutingUserMetricLogger
            builder.Register(c =>
            {
                Routing.UserMetricLogger = new NullRoutingUserMetricLogger();
                return Routing.UserMetricLogger;
            })
                .As<IRoutingUserMetricLogger>()
                .SingleInstance();

            // IRoutingUserAnalyticsLogger
            builder.Register(c =>
            {
                Routing.UserAnalyticsLogger = new NullUserAnalyticsLogger();
                return Routing.UserAnalyticsLogger;
            })
                .As<IRoutingUserAnalyticsLogger>()
                .SingleInstance();
        }
    }
}
