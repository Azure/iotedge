// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using Microsoft.Azure.Devices.Routing.Core.Query;

    public interface IRouteCompiler
    {
        Func<IMessage, Bool> Compile(Route route);

        Func<IMessage, Bool> Compile(Route route, RouteCompilerFlags routeCompilerFlags);
    }
}
