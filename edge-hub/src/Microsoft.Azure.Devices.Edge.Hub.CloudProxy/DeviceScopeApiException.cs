// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Net;

    public class DeviceScopeApiException : Exception
    {
        public DeviceScopeApiException(string message, HttpStatusCode statusCode, string content)
            : base(message)
        {
            this.StatusCode = statusCode;
            this.Content = content;
        }

        public HttpStatusCode StatusCode { get; }

        public string Content { get; }

        public override string Message => $"Message: {base.Message}, HttpStatusCode: {this.StatusCode}, Content: {this.Content}";
    }
}
