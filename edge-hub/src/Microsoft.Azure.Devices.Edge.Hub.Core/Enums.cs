// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public enum FeedbackStatus
    {
        Complete,
        Abandon,
        Reject
    }

    public enum EndpointType
    {
        Null, // Store
        Cloud,
        Module
    }
}
