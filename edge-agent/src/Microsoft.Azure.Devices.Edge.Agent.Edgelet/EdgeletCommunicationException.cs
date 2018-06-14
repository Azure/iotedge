// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System;

    public class EdgeletCommunicationException : Exception
    {
        public EdgeletCommunicationException(string message, int statusCode)
            : base(message)
        {
            this.StatusCode = statusCode;
        }

        public int StatusCode { get; }

        public override string ToString() =>
            $"{typeof(EdgeletCommunicationException).FullName}- Message:{this.Message}, StatusCode:{this.StatusCode}, at:{this.StackTrace}";
    }
}
