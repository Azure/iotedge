// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper
{
    using System;

    public class EdgeletCommunicationException : Exception
    {
        public int StatusCode { get; }

        public EdgeletCommunicationException()
        {
        }

        public EdgeletCommunicationException(string message) : base(message)
        {
        }

        public EdgeletCommunicationException(string message, int statusCode) : base($"{message}, StatusCode: {statusCode}")
        {
            this.StatusCode = statusCode;
        }
    }
}
