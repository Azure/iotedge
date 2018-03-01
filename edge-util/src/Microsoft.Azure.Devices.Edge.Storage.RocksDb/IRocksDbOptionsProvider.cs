// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using RocksDbSharp;

    public interface IRocksDbOptionsProvider
    {
        DbOptions GetDbOptions();

        ColumnFamilyOptions GetColumnFamilyOptions();
    }
}
