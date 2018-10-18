// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    /// <summary>
    ///     Common names for concepts in device twin messages.
    /// </summary>
    public static class TwinNames
    {
        public const string Desired = "desired";
        public const string LastUpdated = SystemParameterPrefix + "lastUpdated";
        public const string Metadata = SystemParameterPrefix + "metadata";
        public const string Properties = "properties";
        public const string Reported = "reported";
        public const string RequestId = SystemParameterPrefix + "rid";
        public const string SystemParameterPrefix = "$";
        public const char SystemParameterPrefixChar = '$';
        public const string Version = SystemParameterPrefix + "version";
    }
}
