// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Newtonsoft.Json;

    public class ContentTrust
    {
        [JsonProperty("rootJsonPath")]
        public string RootJsonPath { get; }

        [JsonProperty("rootID")]
        public string RootID { get; }

        [JsonProperty("rootCertificatePath")]
        public string RootCertificatePath { get; }

        [JsonProperty("disableTOFU")]
        public bool DisableTOFU { get; }
    }
}
