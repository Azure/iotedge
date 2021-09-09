// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.IO;

    public class EdgeHubIOException : IOException
    {
        public EdgeHubIOException(string message)
            : this(message, null)
        {
        }

        public EdgeHubIOException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
