// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using System;
    using System.Collections.Generic;
    using RocksDbSharp;

    interface IRocksDb : IDisposable
    {
        IEnumerable<string> ListColumnFamilies();

        ColumnFamilyHandle GetColumnFamily(string columnFamilyName);

        ColumnFamilyHandle CreateColumnFamily(ColumnFamilyOptions columnFamilyOptions, string columnFamilyName);

        void DropColumnFamily(string columnFamilyName);

        byte[] Get(byte[] key, ColumnFamilyHandle handle);

        void Put(byte[] key, byte[] value, ColumnFamilyHandle handle);

        void Remove(byte[] key, ColumnFamilyHandle handle);

        Iterator NewIterator(ColumnFamilyHandle handle, ReadOptions readOptions);

        Iterator NewIterator(ColumnFamilyHandle handle);

        void Compact(ColumnFamilyHandle handle);
    }
}
