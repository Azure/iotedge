// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    public interface IRoutingUserAnalyticsLogger
    {
        void LogOrphanedMessage(string iotHubName, IMessage message);

        void LogDroppedMessage(string iotHubName, IMessage message, string endpointName, FailureKind failureKind);

        void LogInvalidMessage(string iotHubName, IMessage message, FailureKind failureKind);

        void LogUnhealthyEndpoint(string iotHubName, string endpointName, FailureKind failureKind);

        void LogDeadEndpoint(string iotHubName, string endpointName);

        void LogHealthyEndpoint(string iotHubName, string endpointName);

        void LogUndefinedRouteEvaluation(IMessage message, Route route);

        void LogRouteEvaluationError(IMessage message, Route route, Exception ex);
    }
}
