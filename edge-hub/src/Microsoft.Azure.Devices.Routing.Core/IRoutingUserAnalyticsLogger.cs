// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    public interface IRoutingUserAnalyticsLogger
    {
        void LogDeadEndpoint(string iotHubName, string endpointName);

        void LogDroppedMessage(string iotHubName, IMessage message, string endpointName, FailureKind failureKind);

        void LogHealthyEndpoint(string iotHubName, string endpointName);

        void LogInvalidMessage(string iotHubName, IMessage message, FailureKind failureKind);

        void LogOrphanedMessage(string iotHubName, IMessage message);

        void LogRouteEvaluationError(IMessage message, Route route, Exception ex);

        void LogUndefinedRouteEvaluation(IMessage message, Route route);

        void LogUnhealthyEndpoint(string iotHubName, string endpointName, FailureKind failureKind);
    }
}
