// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public static class Constants
    {
        public static class SystemPropertyValues
        {
            public static readonly Dictionary<string, Encoding> ContentEncodings = new Dictionary<string, Encoding>(StringComparer.OrdinalIgnoreCase)
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
    }
}
