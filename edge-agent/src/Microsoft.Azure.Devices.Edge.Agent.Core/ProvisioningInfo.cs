// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.ComponentModel;
    using Newtonsoft.Json;

    public class ProvisioningInfo
    {
        public ProvisioningInfo(string provisioningType, bool dynamicReprovisioning, bool alwaysReprovisionOnStartup)
        {
            this.Type = provisioningType;
            this.DynamicReprovisioning = dynamicReprovisioning;
            this.AlwaysReprovisionOnStartup = alwaysReprovisionOnStartup;
        }

        public ProvisioningInfo(string provisioningType, bool dynamicReprovisioning)
        {
            this.Type = provisioningType;
            this.DynamicReprovisioning = dynamicReprovisioning;
            this.AlwaysReprovisionOnStartup = true;
        }

        public string Type { get; }

        public bool DynamicReprovisioning { get; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool AlwaysReprovisionOnStartup { get; }

        public static ProvisioningInfo Empty { get; } = new ProvisioningInfo(string.Empty, false);
    }
}
