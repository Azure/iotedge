// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class KubernetesServiceOptions
    {
        public KubernetesServiceOptions(string type, string loadBalancerIP)
        {
            PortMapServiceType serviceType;
            this.Type = Enum.TryParse(type, true, out serviceType) ? Option.Some(serviceType) : Option.None<PortMapServiceType>();
            this.LoadBalancerIP = Option.Maybe(loadBalancerIP);
        }

        [JsonProperty("Type", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<PortMapServiceType>))]
        public Option<PortMapServiceType> Type { get; }

        [JsonProperty("LoadBalancerIP", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> LoadBalancerIP { get; }
    }
}
