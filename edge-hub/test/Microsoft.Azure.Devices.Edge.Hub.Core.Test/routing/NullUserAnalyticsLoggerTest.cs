// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Xunit;

    [Unit]
    public class NullUserAnalyticsLoggerTest
    {
        [Fact]
        public void LogOrphanedMessage()
        {
            var nullUserAnalyticsLogger = new NullUserAnalyticsLogger();
            nullUserAnalyticsLogger.LogOrphanedMessage(null, null);
        }

        [Fact]
        public void LogDroppedMessage()
        {
            var nullUserAnalyticsLogger = new NullUserAnalyticsLogger();
            nullUserAnalyticsLogger.LogDroppedMessage(null, null, null, FailureKind.None);
        }

        [Fact]
        public void LogInvalidMessage()
        {
            var nullUserAnalyticsLogger = new NullUserAnalyticsLogger();
            nullUserAnalyticsLogger.LogInvalidMessage(null, null, FailureKind.None);
        }

        [Fact]
        public void LogUnhealthyEndpoint()
        {
            var nullUserAnalyticsLogger = new NullUserAnalyticsLogger();
            nullUserAnalyticsLogger.LogUnhealthyEndpoint(null, null, FailureKind.None);
        }

        [Fact]
        public void LogDeadEndpoint()
        {
            var nullUserAnalyticsLogger = new NullUserAnalyticsLogger();
            nullUserAnalyticsLogger.LogDeadEndpoint(null, null);
        }

        [Fact]
        public void LogHealthyEndpoint()
        {
            var nullUserAnalyticsLogger = new NullUserAnalyticsLogger();
            nullUserAnalyticsLogger.LogHealthyEndpoint(null, null);
        }

        [Fact]
        public void LogUndefinedRouteEvaluation()
        {
            var nullUserAnalyticsLogger = new NullUserAnalyticsLogger();
            nullUserAnalyticsLogger.LogUndefinedRouteEvaluation(null, null);
        }

        [Fact]
        public void LogRouteEvaluationError()
        {
            var nullUserAnalyticsLogger = new NullUserAnalyticsLogger();
            nullUserAnalyticsLogger.LogRouteEvaluationError(null, null, null);
        }
    }
}
