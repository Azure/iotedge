// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    public interface ISizedDbStore : IDbStore
    {
        long DbSizeInBytes { get; }
    }
}
