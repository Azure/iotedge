// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Util
{
    using System;

    public interface ISystemTime
    {
        DateTime UtcNow { get; }
    }
}