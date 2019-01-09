// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class EdgeHubMessageTooLargeException : Exception
    {
        public EdgeHubMessageTooLargeException(string message)
            : base(message)
        {
        }

        public EdgeHubMessageTooLargeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
