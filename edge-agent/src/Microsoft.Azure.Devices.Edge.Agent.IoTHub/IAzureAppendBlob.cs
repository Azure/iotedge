// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;

    public interface IAzureAppendBlob
    {
        string Name { get; }

        Task AppendByteArray(ArraySegment<byte> bytes);
    }
}
