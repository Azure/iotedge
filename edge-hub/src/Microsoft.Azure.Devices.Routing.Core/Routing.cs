// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using Microsoft.Extensions.Logging;

    public static class Routing
    {
        static IRoutingPerfCounter perfCounter;
        static IRoutingUserAnalyticsLogger userAnalyticsLogger;
        static IRoutingUserMetricLogger userMetricLogger;

        // TODO figure out how to attach this to other logger config
        public static ILoggerFactory LoggerFactory { get; } = new LoggerFactory();

        const int EventIdStart = 9000;

        public static class EventIds
        {
            public const int Dispatcher = EventIdStart;
            public const int Evaluator = EventIdStart + 100;
            public const int Router = EventIdStart + 200;

            // Checkpointers
            public const int Checkpointer = EventIdStart + 300;
            public const int MasterCheckpointer = EventIdStart + 400;

            // Endpoints
            public const int EndpointExecutorFsm = EventIdStart + 500;
            public const int AsyncEndpointExecutor = EventIdStart + 600;
            public const int SyncEndpointExecutor = EventIdStart + 700;

            // Query
            public const int BodyQuery = EventIdStart + 800;
            public const int TwinChangeIncludes = EventIdStart + 900;

            // Services
            public const int FilteringRoutingService = EventIdStart + 1000;
            public const int FrontendRoutingService = EventIdStart + 1100;
        }


        public static IRoutingPerfCounter PerfCounter
        {
            get => perfCounter ?? NullRoutingPerfCounter.Instance;
            set => perfCounter = value;
        }

        public static IRoutingUserMetricLogger UserMetricLogger
        {
            get => userMetricLogger ?? NullRoutingUserMetricLogger.Instance;
            set => userMetricLogger = value;
        }

        public static IRoutingUserAnalyticsLogger UserAnalyticsLogger
        {
            get => userAnalyticsLogger ?? NullUserAnalyticsLogger.Instance;
            set => userAnalyticsLogger = value;
        }
    }
}