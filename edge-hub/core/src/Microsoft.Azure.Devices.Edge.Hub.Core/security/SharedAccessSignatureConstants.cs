// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    static class SharedAccessSignatureConstants
    {
        public const int MaxKeyNameLength = 256;
        public const int MaxKeyLength = 256;
        public const string SharedAccessSignature = "SharedAccessSignature";
        public const string AudienceFieldName = "sr";
        public const string SignatureFieldName = "sig";
        public const string KeyNameFieldName = "skn";
        public const string ExpiryFieldName = "se";
        public const string SignedResourceFullFieldName = SharedAccessSignature + " " + AudienceFieldName;
        public const string KeyValueSeparator = "=";
        public const string PairSeparator = "&";
        public static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);
    }
}
