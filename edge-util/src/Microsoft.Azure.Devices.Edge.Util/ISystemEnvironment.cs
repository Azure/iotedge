// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    public interface ISystemEnvironment
    {
        bool Is32BitOperatingSystem { get; }

        bool Is32BitProcess { get; }
    }
}
