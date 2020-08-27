// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;

    public interface IMessage : IDisposable
    {
        byte[] Body { get; }

        IDictionary<string, string> Properties { get; }

        IDictionary<string, string> SystemProperties { get; }

        // Used to report the priority at which the message
        // is being processed. This value is not valid at
        // at routing and enqueue time, and is strictly
        // meant to be used for metrics.
        uint ProcessedPriority { get; }

        long EnqueuedTimestamp { get; }
    }
}
