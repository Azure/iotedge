// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    /// <summary>
    ///     Common names for concepts in device twin messages.
    /// </summary>
    public static class TwinNames
    {
        public const char SystemParameterPrefixChar = '$';
        public const string SystemParameterPrefix = "$";
        public const string Properties = "properties";
        public const string Reported = "reported";
        public const string Desired = "desired";
        public const string Metadata = SystemParameterPrefix + "metadata";
        public const string LastUpdated = SystemParameterPrefix + "lastUpdated";
        public const string Version = SystemParameterPrefix + "version";
        public const string RequestId = SystemParameterPrefix + "rid";
    }
}
