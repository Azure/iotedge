// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;

    /// <summary>
    /// The exception that is thrown when communication fails with HSM HTTP server.
    /// </summary>
    public class HttpHsmCommunicationException : Exception
    {
        /// <summary>
        /// Status code of the communication failure.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpHsmCommunicationException"/> class with the message string.
        /// </summary>
        /// <param name="message">A description of the error. The content of message is intended to be understood by humans.</param>
        /// <param name="statusCode">Status code of the communication failure.</param>
        public HttpHsmCommunicationException(string message, int statusCode) : base($"{message}, StatusCode: {statusCode}")
        {
            this.StatusCode = statusCode;
        }
    }
}
