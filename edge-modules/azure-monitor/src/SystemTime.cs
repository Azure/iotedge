// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;

    public class SystemTime : ISystemTime
    {
        public static SystemTime Instance { get; } = new SystemTime();

        public DateTime UtcNow => DateTime.UtcNow;
    }
}
