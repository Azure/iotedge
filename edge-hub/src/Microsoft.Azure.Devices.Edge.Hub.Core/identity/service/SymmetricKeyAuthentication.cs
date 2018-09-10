// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class SymmetricKeyAuthentication
    {
        [JsonConstructor]
        public SymmetricKeyAuthentication(string primaryKey, string secondaryKey)
        {
            this.PrimaryKey = Preconditions.CheckNonWhiteSpace(primaryKey, nameof(primaryKey));
            this.SecondaryKey = Preconditions.CheckNonWhiteSpace(secondaryKey, nameof(secondaryKey));
        }

        [JsonProperty("primaryKey")]
        public string PrimaryKey { get; }

        [JsonProperty("secondaryKey")]
        public string SecondaryKey { get; }
    }
}
