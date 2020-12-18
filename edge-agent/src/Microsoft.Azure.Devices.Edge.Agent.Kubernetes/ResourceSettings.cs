// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// K8s Resource requirements as read in by autofac
    /// </summary>
    public class ResourceSettings
    {
        public Dictionary<string, string> Limits { get; set; }
        public Dictionary<string, string> Requests { get; set; }

        public V1ResourceRequirements ToResourceRequirements()
        {
            Dictionary<string, ResourceQuantity> limits = Option.Maybe(this.Limits).Map(limitMap =>
                limitMap.ToDictionary(pair => pair.Key, pair => new ResourceQuantity(pair.Value))).OrDefault();
            Dictionary<string, ResourceQuantity> requests = Option.Maybe(this.Requests).Map(limitMap =>
                limitMap.ToDictionary(pair => pair.Key, pair => new ResourceQuantity(pair.Value))).OrDefault();
            return new V1ResourceRequirements(limits, requests);
        }
    }
}
