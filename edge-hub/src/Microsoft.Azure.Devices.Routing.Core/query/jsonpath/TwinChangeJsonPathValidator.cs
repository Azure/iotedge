// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.JsonPath
{
    using System;
    using System.Globalization;
    using System.Linq;

    static class TwinChangeJsonPathValidator
    {
        static readonly string[] TwinChangeJsonSupportedPrefixes =
        {
            "properties.reported.",
            "properties.desired.",
            "tags."
        };

        static readonly Lazy<string> ErrorMessageFormat = new Lazy<string>(
            () =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}{1}.",
                    "'{0}' is not supported. Supported prefixes: ",
                    TwinChangeJsonSupportedPrefixes.Select(
                        (s) =>
                            string.Format(CultureInfo.InvariantCulture, "'{0}'", s)).Aggregate(
                        (s1, s2) => string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}, {1}",
                            s1,
                            s2))));

        public static bool IsSupportedJsonPath(string jsonPath, out string errorDetails)
        {
            if (!JsonPathValidator.IsSupportedJsonPath(jsonPath, out errorDetails))
            {
                return false;
            }

            // Check if prefixes match
            if (!TwinChangeJsonSupportedPrefixes.Any(s => jsonPath.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
            {
                errorDetails = string.Format(CultureInfo.InvariantCulture, ErrorMessageFormat.Value, jsonPath);
                return false;
            }

            return true;
        }
    }
}
