// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    public enum FailureKind
    {
        None = 0,
        Transient = 1,
        InternalError = 2,
        Unauthorized = 3,
        Throttled = 4,
        Timeout = 5,

        // Service Bus
        MaxMessageSizeExceeded = 20,

        // Queues and Topics
        PartitioningAndDuplicateDetectionNotSupported = 40,
        SessionfulEntityNotSupported = 41,
        NoMatchingSubscriptionsForMessage = 42,
        EndpointExternallyDisabled = 43,
    }
}
