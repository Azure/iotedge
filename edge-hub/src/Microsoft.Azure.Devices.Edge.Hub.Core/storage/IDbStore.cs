// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Storage
{
    public interface IDbStore : IKeyValueStore<byte[], byte[]>
    { }
}
