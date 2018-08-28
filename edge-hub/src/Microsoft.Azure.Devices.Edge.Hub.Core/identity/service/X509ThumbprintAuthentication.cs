// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class X509ThumbprintAuthentication
    {
        [JsonConstructor]
        public X509ThumbprintAuthentication(string primaryThumbprint, string secondaryThumbprint)
        {
            this.PrimaryThumbprint = Preconditions.CheckNonWhiteSpace(primaryThumbprint, nameof(primaryThumbprint));
            this.SecondaryThumbprint = Preconditions.CheckNonWhiteSpace(secondaryThumbprint, nameof(secondaryThumbprint));
        }

        [JsonProperty("primaryThumbprint")]
        public string PrimaryThumbprint { get; }

        [JsonProperty("secondaryThumbprint")]
        public string SecondaryThumbprint { get; }
    }
}
