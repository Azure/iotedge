// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class SystemModules : IEquatable<SystemModules>
    {
        [JsonConstructor]
        public SystemModules(IEdgeAgentModule edgeAgent, IEdgeHubModule edgeHub)
        {
            this.EdgeAgent = Option.Maybe(edgeAgent);
            this.EdgeHub = Option.Maybe(edgeHub);
        }

        public SystemModules(Option<IEdgeAgentModule> edgeAgent, Option<IEdgeHubModule> edgeHub)
        {
            this.EdgeAgent = edgeAgent;
            this.EdgeHub = edgeHub;
        }

        [JsonProperty(PropertyName = "edgeHub")]
        [JsonConverter(typeof(OptionConverter<IEdgeHubModule>))]
        public Option<IEdgeHubModule> EdgeHub { get; }

        [JsonProperty(PropertyName = "edgeAgent")]
        [JsonConverter(typeof(OptionConverter<IEdgeAgentModule>))]
        public Option<IEdgeAgentModule> EdgeAgent { get; }

        public override bool Equals(object obj) => this.Equals(obj as SystemModules);

        public bool Equals(SystemModules other)
        {
            return other != null &&
                EqualityComparer<Option<IEdgeHubModule>>.Default.Equals(this.EdgeHub, other.EdgeHub) &&
                EqualityComparer<Option<IEdgeAgentModule>>.Default.Equals(this.EdgeAgent, other.EdgeAgent);
        }

        public override int GetHashCode()
        {
            int hashCode = -874519432;
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<IEdgeHubModule>>.Default.GetHashCode(this.EdgeHub);
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<IEdgeAgentModule>>.Default.GetHashCode(this.EdgeAgent);
            return hashCode;
        }

        public static bool operator ==(SystemModules modules1, SystemModules modules2)
        {
            return EqualityComparer<SystemModules>.Default.Equals(modules1, modules2);
        }

        public static bool operator !=(SystemModules modules1, SystemModules modules2)
        {
            return !(modules1 == modules2);
        }

        public SystemModules Clone() => new SystemModules(this.EdgeAgent, this.EdgeHub);
    }
}
