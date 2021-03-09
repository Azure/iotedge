// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;

    public interface ISystemTime
    {
        DateTime UtcNow { get; }
    }
}
