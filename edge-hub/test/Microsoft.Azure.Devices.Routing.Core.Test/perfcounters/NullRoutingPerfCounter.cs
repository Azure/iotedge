// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Test.PerfCounters
{
    public class NullRoutingPerfCounter : IRoutingPerfCounter
    {
        public bool LogEventProcessingLatency(string iotHubName, string endpointName, string endpointType, string status, long latencyInMs, out string errorString)
        {
            errorString = string.Empty;
            return true;
        }
        public bool LogE2EEventProcessingLatency(string iotHubName, string endpointName, string endpointType, string status, long latencyInMs, out string errorString)
        {
            errorString = string.Empty;
            return true;
        }

        public bool LogEventsProcessed(string iotHubName, string endpointName, string endpointType, string status, long count, out string errorString)
        {
            errorString = string.Empty;
            return true;
        }

        public bool LogInternalEventHubReadLatency(string iotHubName, long partitionId, bool success, long latencyInMs, out string errorString)
        {
            errorString = string.Empty;
            return true;
        }

        public bool LogInternalEventHubEventsRead(string iotHubName, long partitionId, bool success, long count, out string errorString)
        {
            errorString = string.Empty;
            return true;
        }

        public bool LogInternalProcessingLatency(string iotHubName, long partitionId, bool success, long latencyInMs, out string errorString)
        {
            errorString = string.Empty;
            return true;
        }

        public bool LogExternalWriteLatency(string iotHubName, string endpointName, string endpointType, bool success, long latencyInMs, out string errorString)
        {
            errorString = string.Empty;
            return true;
        }

        public bool LogMessageEndpointsMatched(string iotHubName, string messageSource, long endpointsEvaluated, out string errorString)
        {
            errorString = string.Empty;
            return true;
        }

        public bool LogUnmatchedMessages(string iotHubName, string messageSource, long unmatchedMessages, out string errorString)
        {
            errorString = string.Empty;
            return true;
        }

        public bool LogCheckpointStoreLatency(string iotHubName, string checkpointStoreType, string checkpointerId, string operationName, string operationStatus, long latencyInMs, out string errorString)
        {
            errorString = string.Empty;
            return true;
        }

        public bool LogOperationResult(string iotHubName, string operationName, string operationStatus, long operationCount, out string errorString)
        {
            errorString = string.Empty;
            return true;
        }
    }
}