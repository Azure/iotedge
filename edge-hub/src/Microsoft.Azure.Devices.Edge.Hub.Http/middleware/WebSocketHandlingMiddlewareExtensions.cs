// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Middleware
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public static class WebSocketHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseWebSocketHandlingMiddleware(this IApplicationBuilder builder, IWebSocketListenerRegistry webSocketListenerRegistry)
        {
            return builder.UseMiddleware<WebSocketHandlingMiddleware>(webSocketListenerRegistry);
        }
    }
}
