// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
