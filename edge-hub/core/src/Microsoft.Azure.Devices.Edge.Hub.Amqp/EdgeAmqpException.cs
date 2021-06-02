// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using Microsoft.Azure.Devices.Common.Exceptions;

    public class EdgeAmqpException : Exception
    {
        public EdgeAmqpException(string message, ErrorCode errorCode)
            : this(message, errorCode, null)
        {
        }

        public EdgeAmqpException(string message, ErrorCode errorCode, Exception innerException)
            : base(message, innerException)
        {
            this.ErrorCode = errorCode;
        }

        public ErrorCode ErrorCode { get; }
    }
}
