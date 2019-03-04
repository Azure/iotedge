// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using Autofac;

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
            builder.Register(
                    c =>
                    {
                        Routing.PerfCounter = NullRoutingPerfCounter.Instance;
                        return Routing.PerfCounter;
                    })
                .As<IRoutingPerfCounter>()
                .SingleInstance();

            // IRoutingUserMetricLogger
            builder.Register(
                    c =>
                    {
                        Routing.UserMetricLogger = NullRoutingUserMetricLogger.Instance;
                        return Routing.UserMetricLogger;
                    })
                .As<IRoutingUserMetricLogger>()
                .SingleInstance();

            // IRoutingUserAnalyticsLogger
            builder.Register(
                    c =>
                    {
                        Routing.UserAnalyticsLogger = NullUserAnalyticsLogger.Instance;
                        return Routing.UserAnalyticsLogger;
                    })
                .As<IRoutingUserAnalyticsLogger>()
                .SingleInstance();
        }
    }
}
