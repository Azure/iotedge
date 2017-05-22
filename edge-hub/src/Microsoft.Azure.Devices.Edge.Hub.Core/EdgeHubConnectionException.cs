// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class EdgeHubConnectionException : Exception
    {
        public EdgeHubConnectionException(string message)
            : this(message, null)
        { }

        public EdgeHubConnectionException(string message, Exception innerException)            
        : base(message, innerException)
        { }
    }
}