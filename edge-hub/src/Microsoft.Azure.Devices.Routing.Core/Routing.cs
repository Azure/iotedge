// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    public static class Routing
    {
        static IRoutingPerfCounter perfCounter;
        static IRoutingUserAnalyticsLogger userAnalyticsLogger;
        static IRoutingUserMetricLogger userMetricLogger;

        //public static ILog Log { get; } = new Log(LogSinkFactory.CreateFromEventSource<RoutingBackendEventSource>());

        public static IRoutingPerfCounter PerfCounter
        {
            get
            {
                if (perfCounter == null)
                {
                    throw new InvalidOperationException("PerfCounter is not initialized.");
                }

                return perfCounter;
            }

            set { perfCounter = value; }
        }

        public static IRoutingUserMetricLogger UserMetricLogger
        {
            get
            {
                if (userMetricLogger == null)
                {
                    throw new InvalidOperationException("UserMetricLogger is not initialized.");
                }

                return userMetricLogger;
            }

            set { userMetricLogger = value; }
        }

        public static IRoutingUserAnalyticsLogger UserAnalyticsLogger
        {
            get
            {
                if (userAnalyticsLogger == null)
                {
                    throw new InvalidOperationException("UserAnalyticsLogger is not initialized.");
                }

                return userAnalyticsLogger;
            }

            set { userAnalyticsLogger = value; }
        }
    }
}