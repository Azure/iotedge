// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class NullUserAnalyticsLoggerTest
    {
        [Fact]
        public void LogOrphanedMessage()
        {
            NullUserAnalyticsLogger.Instance.LogOrphanedMessage(null, null);
        }

        [Fact]
        public void LogDroppedMessage()
        {
            NullUserAnalyticsLogger.Instance.LogDroppedMessage(null, null, null, FailureKind.None);
        }

        [Fact]
        public void LogInvalidMessage()
        {
            NullUserAnalyticsLogger.Instance.LogInvalidMessage(null, null, FailureKind.None);
        }

        [Fact]
        public void LogUnhealthyEndpoint()
        {
            NullUserAnalyticsLogger.Instance.LogUnhealthyEndpoint(null, null, FailureKind.None);
        }

        [Fact]
        public void LogDeadEndpoint()
        {
            NullUserAnalyticsLogger.Instance.LogDeadEndpoint(null, null);
        }

        [Fact]
        public void LogHealthyEndpoint()
        {
            NullUserAnalyticsLogger.Instance.LogHealthyEndpoint(null, null);
        }

        [Fact]
        public void LogUndefinedRouteEvaluation()
        {
            NullUserAnalyticsLogger.Instance.LogUndefinedRouteEvaluation(null, null);
        }

        [Fact]
        public void LogRouteEvaluationError()
        {
            NullUserAnalyticsLogger.Instance.LogRouteEvaluationError(null, null, null);
        }
    }
}
