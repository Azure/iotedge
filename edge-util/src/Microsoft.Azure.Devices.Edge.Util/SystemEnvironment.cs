// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    public class SystemEnvironment : ISystemEnvironment
    {
        public bool Is32BitOperatingSystem => !Environment.Is64BitOperatingSystem;

        public bool Is32BitProcess => !Environment.Is64BitProcess;
    }
}
