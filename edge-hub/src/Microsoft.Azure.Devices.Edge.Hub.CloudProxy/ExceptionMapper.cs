// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class ExceptionMapper
    {
        const string FailOverMessage = "(condition='com.microsoft:iot-hub-not-found-error')";

        public static Exception GetEdgeException(this Exception sdkException, string operation)
        {
            Preconditions.CheckNonWhiteSpace(operation, nameof(operation));
            if (sdkException != null)
            {
                if (sdkException is ConnectTimeoutException connectTimeoutException)
                {
                    return new TimeoutException($"Operation {operation} timed out", connectTimeoutException);
                }
            }

            return sdkException;
        }

        public static bool IsFailOver(this Exception ex)
        {
            var isFailOver = ex is IotHubException
                          && ex.InnerException != null
                          && !string.IsNullOrEmpty(ex.InnerException.Message)
                          && ex.InnerException.Message.Contains(FailOverMessage);

            return isFailOver;
        }
    }
}
