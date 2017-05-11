// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    public class NullUserAnalyticsLogger : IRoutingUserAnalyticsLogger
    {
        public void LogDeadEndpoint(string iotHubName, string endpointName)
        {
        }

        public void LogDroppedMessage(string iotHubName, IMessage message, string endpointName, FailureKind failureKind)
        {
        }

        public void LogHealthyEndpoint(string iotHubName, string endpointName)
        {
        }

        public void LogInvalidMessage(string iotHubName, IMessage message, FailureKind failureKind)
        {
        }

        public void LogOrphanedMessage(string iotHubName, IMessage message)
        {
        }

        public void LogUndefinedRouteEvaluation(IMessage message, Route route)
        {
        }

        public void LogRouteEvaluationError(IMessage message, Route route, System.Exception ex)
        {
        }

        public void LogUnhealthyEndpoint(string iotHubName, string endpointName, FailureKind failureKind)
        {
        }
    }
}
