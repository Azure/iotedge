// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
