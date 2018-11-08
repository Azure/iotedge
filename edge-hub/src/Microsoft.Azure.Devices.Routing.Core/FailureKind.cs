// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
