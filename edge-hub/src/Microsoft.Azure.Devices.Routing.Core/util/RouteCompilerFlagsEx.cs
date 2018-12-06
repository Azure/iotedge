// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Util
{
    using Microsoft.Azure.Devices.Routing.Core.Query;

    public static class RouteCompilerFlagsEx
    {
        public static RouteCompilerFlags Create(bool isTwinChangeIncludesEnabled, bool isBodyQueryEnabled)
        {
            var flags = RouteCompilerFlags.None;

            if (isTwinChangeIncludesEnabled)
            {
                flags |= RouteCompilerFlags.TwinChangeIncludes;
            }

            if (isBodyQueryEnabled)
            {
                flags |= RouteCompilerFlags.BodyQuery;
            }

            return flags;
        }

        public static bool IsFlagEnabled(this RouteCompilerFlags routeCompilerFlags, RouteCompilerFlags flagToCheck)
        {
            return (routeCompilerFlags & flagToCheck) != 0;
        }
    }
}
