// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    public class SystemTime : ISystemTime
    {
        public static SystemTime Instance { get; } = new SystemTime();

        public DateTime UtcNow => DateTime.UtcNow;
    }
}
