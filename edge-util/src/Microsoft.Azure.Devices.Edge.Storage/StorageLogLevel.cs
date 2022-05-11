// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    // Ref: https://github.com/facebook/rocksdb/blob/main/java/src/main/java/org/rocksdb/InfoLogLevel.java
    public enum StorageLogLevel
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