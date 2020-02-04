// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class KubernetesModuleVolumeSpec
    {
        public KubernetesModuleVolumeSpec(V1Volume volume, IReadOnlyList<V1VolumeMount> volumeMounts)
        {
            this.Volume = Option.Maybe(volume);
            this.VolumeMounts = Option.Maybe(volumeMounts);
        }

        [JsonProperty("Volume", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<V1Volume>))]
        public Option<V1Volume> Volume { get; }

        [JsonProperty("VolumeMounts", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IReadOnlyList<V1VolumeMount>>))]
        public Option<IReadOnlyList<V1VolumeMount>> VolumeMounts { get; }
    }
}
