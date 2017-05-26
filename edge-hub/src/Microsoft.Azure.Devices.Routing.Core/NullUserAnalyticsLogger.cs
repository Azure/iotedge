// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    public class NullUserAnalyticsLogger : IRoutingUserAnalyticsLogger
    {
        public static NullUserAnalyticsLogger Instance { get; } = new NullUserAnalyticsLogger();

        NullUserAnalyticsLogger()
        {
        }

        public void LogOrphanedMessage(string iotHubName, IMessage message)
        {            
        }

        public void LogDroppedMessage(string iotHubName, IMessage message, string endpointName, FailureKind failureKind)
        {            
        }

        public void LogInvalidMessage(string iotHubName, IMessage message, FailureKind failureKind)
        {            
        }

        public void LogUnhealthyEndpoint(string iotHubName, string endpointName, FailureKind failureKind)
        {            
        }

        public void LogDeadEndpoint(string iotHubName, string endpointName)
        {            
        }

        public void LogHealthyEndpoint(string iotHubName, string endpointName)
        {          
        }

        public void LogUndefinedRouteEvaluation(IMessage message, Route route)
        {            
        }

        public void LogRouteEvaluationError(IMessage message, Route route, Exception ex)
        {            
        }
    }
}