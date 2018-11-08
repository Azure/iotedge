// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;

    using Microsoft.Azure.Devices.Common.Exceptions;

    public class EdgeHubAmqpException : Exception
    {
        public EdgeHubAmqpException(string message, ErrorCode errorCode)
            : this(message, errorCode, null)
        {
        }

        public EdgeHubAmqpException(string message, ErrorCode errorCode, Exception innerException)
            : base(message, innerException)
        {
            this.ErrorCode = errorCode;
        }

        public ErrorCode ErrorCode { get; }
    }
}
