// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

        public string Content { get; }

        public override string Message => $"Message: {base.Message}, HttpStatusCode: {this.StatusCode}, Content: {this.Content}";

        public HttpStatusCode StatusCode { get; }
    }
}
