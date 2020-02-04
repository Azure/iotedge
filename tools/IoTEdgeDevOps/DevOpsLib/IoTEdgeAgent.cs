// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using DevOpsLib.VstsModels;

    public class IoTEdgeAgent : IEquatable<IoTEdgeAgent>, ICloneable
    {
        const string AgentGroupCapabilityKey = "agent-group";

        readonly int id;
        readonly string name;
        readonly string version;
        readonly VstsAgentStatus status;
        readonly bool enabled;
        readonly HashSet<AgentCapability> systemCapabilities;
        readonly HashSet<AgentCapability> userCapabilities;

        public IoTEdgeAgent(int id, string name, string version, VstsAgentStatus status, bool enabled, HashSet<AgentCapability> systemCapabilities, HashSet<AgentCapability> userCapabilities)
        {
            ValidationUtil.ThrowIfNonPositive(id, nameof(id));
            ValidationUtil.ThrowIfNullOrWhiteSpace(name, nameof(name));
            ValidationUtil.ThrowIfNullOrWhiteSpace(version, nameof(version));
            ValidationUtil.ThrowIfNulOrEmptySet(systemCapabilities, nameof(systemCapabilities));

            this.id = id;
            this.name = name;
            this.version = version;
            this.status = status;
            this.enabled = enabled;
            this.systemCapabilities = systemCapabilities;
            this.userCapabilities = userCapabilities ?? new HashSet<AgentCapability>();
        }

        public int Id => this.id;

        public string Name => this.name;

        public string Version => this.version;

        public bool Enabled => this.enabled;

        public VstsAgentStatus Status => this.status;

        public ImmutableHashSet<AgentCapability> SystemCapabilities => this.systemCapabilities.ToImmutableHashSet();

        public ImmutableHashSet<AgentCapability> UserCapabilities => this.userCapabilities.ToImmutableHashSet();

        public bool IsAvailable => this.enabled && this.status == VstsAgentStatus.Online;

        public static IoTEdgeAgent Create(VstsAgent vstsAgent)
        {
            ValidationUtil.ThrowIfNull(vstsAgent, nameof(vstsAgent));

            HashSet<AgentCapability> systemCapabilities = vstsAgent.SystemCapabilities?.Select(c => new AgentCapability(c.Key, c.Value)).ToHashSet();
            HashSet<AgentCapability> userCapabilities = vstsAgent.UserCapabilities?.Select(c => new AgentCapability(c.Key, c.Value)).ToHashSet();

            return new IoTEdgeAgent(
                vstsAgent.Id,
                vstsAgent.Name,
                vstsAgent.Version,
                vstsAgent.Status,
                vstsAgent.Enabled,
                systemCapabilities,
                userCapabilities);
        }

        public bool Match(IEnumerable<AgentCapability> capabilities)
        {
            var allCapabilities = this.systemCapabilities.Union(this.userCapabilities);
            return capabilities.All(allCapabilities.Contains);
        }

        public string GetAgentGroup()
        {
            return this.userCapabilities.Where(c => c.Name.Equals(AgentGroupCapabilityKey)).Select(c => c.Value).FirstOrDefault() ?? string.Empty;
        }

        public object Clone()
        {
            return new IoTEdgeAgent(
                this.Id,
                this.Name,
                this.Version,
                this.Status,
                this.Enabled,
                new HashSet<AgentCapability>(this.systemCapabilities.Select(c => (AgentCapability)c.Clone())),
                new HashSet<AgentCapability>(this.userCapabilities.Select(c => (AgentCapability)c.Clone())));
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        public bool Equals(IoTEdgeAgent other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.id == other.id &&
                string.Equals(this.name, other.name, StringComparison.Ordinal) &&
                string.Equals(this.version, other.version, StringComparison.Ordinal) &&
                this.status == other.status &&
                this.enabled == other.enabled &&
                this.systemCapabilities.SetEquals(other.systemCapabilities) &&
                this.userCapabilities.SetEquals(other.userCapabilities);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((IoTEdgeAgent)obj);
        }
    }
}
