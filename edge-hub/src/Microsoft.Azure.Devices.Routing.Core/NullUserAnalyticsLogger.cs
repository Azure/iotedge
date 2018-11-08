// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    public class NullUserAnalyticsLogger : IRoutingUserAnalyticsLogger
    {
        NullUserAnalyticsLogger()
        {
        }

        public static NullUserAnalyticsLogger Instance { get; } = new NullUserAnalyticsLogger();

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

        public void LogRouteEvaluationError(IMessage message, Route route, Exception ex)
        {
        }

        public void LogUndefinedRouteEvaluation(IMessage message, Route route)
        {
        }

        public void LogUnhealthyEndpoint(string iotHubName, string endpointName, FailureKind failureKind)
        {
        }
    }
}
