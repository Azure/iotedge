// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DirectMethodResponse
    {
        public DirectMethodResponse(string rid, byte[] data, int statusCode)
        {
            this.CorrelationId = rid;
            this.Data = data;
            this.Status = statusCode;
            this.HttpStatusCode = HttpStatusCode.OK;
            this.Exception = Option.None<Exception>();
        }

        public DirectMethodResponse(Exception exception, HttpStatusCode code)
        {
            this.Exception = Option.Some(Preconditions.CheckNotNull(exception, nameof(exception)));
            this.HttpStatusCode = code;
        }

        public Option<Exception> Exception { get; }

        public HttpStatusCode HttpStatusCode { get; }

        public byte[] Data { get; }

        public int Status { get; }

        public string CorrelationId { get; }
    }
}
