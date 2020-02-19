// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    // Ref: https://github.com/facebook/rocksdb/blob/master/java/src/main/java/org/rocksdb/InfoLogLevel.java
    public enum RocksDbInfoLogLevel
    {
        DEBUG = 0,
        INFO,
        WARN,
        ERROR,
        FATAL,
        HEADER,
        NONE
    }
}