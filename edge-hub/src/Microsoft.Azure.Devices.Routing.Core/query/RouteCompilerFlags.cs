// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System;

    [Flags]
    public enum RouteCompilerFlags
    {
        None = 0,
        TwinChangeIncludes = 1 << 0,
        BodyQuery = 1 << 1,

        All = ~0
    }
}
