// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    public interface ISystemTime
    {
        DateTime UtcNow { get; }
    }
}
