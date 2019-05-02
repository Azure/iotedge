// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.IO;

    public class StorageFullException : IOException
    {
        public StorageFullException(string message)
            : base(message)
        {
        }

        public StorageFullException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
