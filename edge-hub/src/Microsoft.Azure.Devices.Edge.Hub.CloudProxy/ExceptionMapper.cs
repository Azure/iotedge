// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class ExceptionMapper
    {
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
    }
}
