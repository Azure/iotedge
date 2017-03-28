// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.IoTHub.Core
{
    using System;
    using System.Collections.Generic;

    public interface IMessage : IDisposable
    {
        byte[] Body { get; }

        IReadOnlyDictionary<string, string> Properties { get; }

        IReadOnlyDictionary<string, string> SystemProperties { get; }
    }
}