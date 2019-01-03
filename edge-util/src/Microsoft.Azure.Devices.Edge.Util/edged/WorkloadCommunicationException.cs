// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;

    public class WorkloadCommunicationException : Exception
    {
        public WorkloadCommunicationException(string message, int statusCode)
            : base(message)
        {
            this.StatusCode = statusCode;
        }

        public int StatusCode { get; }

        public override string ToString() =>
            $"{typeof(WorkloadCommunicationException).FullName}- Message:{this.Message}, StatusCode:{this.StatusCode}, at:{this.StackTrace}";
    }
}
