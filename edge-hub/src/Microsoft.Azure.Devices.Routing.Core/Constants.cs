// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public static class Constants
    {
        public static class SystemPropertyValues
        {
            public static Dictionary<string, Encoding> ContentEncodings = new Dictionary<string, Encoding>(StringComparer.OrdinalIgnoreCase)
            {
                { "utf-8", Encoding.UTF8 },
                { "utf-16", Encoding.Unicode },
                { "utf-32", Encoding.UTF32 }
            };

            public static class ContentType
            {
                public const string Json = "application/json";
            }
        }

        public static class Query
        {
            public static class Builtins
            {
                public const string twin_change_includes = "twin_change_includes";
            }
        }
    }
}
