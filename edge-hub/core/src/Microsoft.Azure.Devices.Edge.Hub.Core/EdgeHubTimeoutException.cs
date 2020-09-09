// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class EdgeHubTimeoutException : Exception
    {
        public EdgeHubTimeoutException(string message)
            : base(message)
        {
        }

        public EdgeHubTimeoutException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
