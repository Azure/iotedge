// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    public interface IRoutingPerfCounter
    {
        bool LogCheckpointStoreLatency(string iotHubName, string checkpointStoreType, string checkpointerId, string operationName, string operationStatus, long latencyInMs, out string errorString);

        bool LogE2EEventProcessingLatency(string iotHubName, string endpointName, string endpointType, string status, long latencyInMs, out string errorString);

        bool LogEventProcessingLatency(string iotHubName, string endpointName, string endpointType, string status, long latencyInMs, out string errorString);

        bool LogEventsProcessed(string iotHubName, string endpointName, string endpointType, string status, long count, out string errorString);

        bool LogExternalWriteLatency(string iotHubName, string endpointName, string endpointType, bool success, long latencyInMs, out string errorString);

        bool LogInternalEventHubEventsRead(string iotHubName, long partitionId, bool success, long count, out string errorString);

        bool LogInternalEventHubReadLatency(string iotHubName, long partitionId, bool success, long latencyInMs, out string errorString);

        bool LogInternalProcessingLatency(string iotHubName, long partitionId, bool success, long latencyInMs, out string errorString);

        bool LogMessageEndpointsMatched(string iotHubName, string messageSource, long endpointsEvaluated, out string errorString);

        bool LogOperationResult(string iotHubName, string operationName, string operationStatus, long operationCount, out string errorString);

        bool LogUnmatchedMessages(string iotHubName, string messageSource, long unmatchedMessages, out string errorString);
    }
}
