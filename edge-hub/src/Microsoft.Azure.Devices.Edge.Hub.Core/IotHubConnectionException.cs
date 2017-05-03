// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class IotHubConnectionException : Exception
    {
        public IotHubConnectionException(string message)
            : this(message, null)
        { }

        public IotHubConnectionException(string message, Exception innerException)            
        : base(message, innerException)
        { }
    }
}