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
    }
}