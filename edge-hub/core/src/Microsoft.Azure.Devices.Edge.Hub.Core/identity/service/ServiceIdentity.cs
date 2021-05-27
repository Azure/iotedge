// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class ServiceIdentity : IEquatable<ServiceIdentity>
    {
        public ServiceIdentity(
            string deviceId,
            string generationId,
            IEnumerable<string> capabilities,
            ServiceAuthentication authentication,
            ServiceIdentityStatus status)
            : this(deviceId, null, null, Enumerable.Empty<string>(), generationId, capabilities, authentication, status)
        {
        }

        [JsonConstructor]
        public ServiceIdentity(
            string deviceId,
            string moduleId,
            string deviceScope,
            IEnumerable<string> parentScopes,
            string generationId,
            IEnumerable<string> capabilities,
            ServiceAuthentication authentication,
            ServiceIdentityStatus status)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Option.Maybe(moduleId);
            this.DeviceScope = Option.Maybe(deviceScope);
            this.Capabilities = Preconditions.CheckNotNull(capabilities, nameof(capabilities));
            this.Authentication = Preconditions.CheckNotNull(authentication, nameof(authentication));
            this.Id = this.ModuleId.Map(m => $"{deviceId}/{moduleId}").GetOrElse(deviceId);
            this.GenerationId = Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
            this.Status = status;

            this.ParentScopes = parentScopes != null
                ? new List<string>(parentScopes)
                : !this.IsEdgeDevice && !string.IsNullOrWhiteSpace(deviceScope) ? new List<string> { deviceScope } : new List<string>();
        }

        [JsonIgnore]
        public string Id { get; }

        [JsonProperty("deviceId")]
        public string DeviceId { get; }

        [JsonProperty("moduleId")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> ModuleId { get; }

        [JsonProperty("deviceScope")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> DeviceScope { get; }

        [JsonProperty("parentScopes")]
        public IList<string> ParentScopes { get; }

        [JsonProperty("capabilities")]
        public IEnumerable<string> Capabilities { get; }

        [JsonIgnore]
        public bool IsEdgeDevice => !this.IsModule && this.Capabilities.Contains(Constants.IotEdgeIdentityCapability);

        [JsonIgnore]
        public bool IsModule => this.ModuleId.HasValue;

        [JsonProperty("authentication")]
        public ServiceAuthentication Authentication { get; }

        [JsonProperty("status")]
        public ServiceIdentityStatus Status { get; }

        [JsonProperty("generationId")]
        public string GenerationId { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return this.Equals((ServiceIdentity)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Id != null ? this.Id.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (this.DeviceId != null ? this.DeviceId.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.ModuleId.GetHashCode();
                hashCode = this.Capabilities != null ? this.Capabilities.Aggregate(hashCode, (acc, item) => acc * 397 ^ item.GetHashCode()) : hashCode;
                hashCode = (hashCode * 397) ^ (this.Authentication != null ? this.Authentication.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)this.Status;
                hashCode = (hashCode * 397) ^ (this.GenerationId != null ? this.GenerationId.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ EqualityComparer<Option<string>>.Default.GetHashCode(this.DeviceScope);
                hashCode = this.ParentScopes != null ? this.ParentScopes.Aggregate(hashCode, (acc, item) => acc * 397 ^ item.GetHashCode()) : hashCode;
                return hashCode;
            }
        }

        public bool Equals(ServiceIdentity other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(this.Id, other.Id)
                && string.Equals(this.DeviceId, other.DeviceId)
                && this.ModuleId.Equals(other.ModuleId)
                && ((this.Capabilities != null && other.Capabilities != null && Enumerable.SequenceEqual(this.Capabilities, other.Capabilities)) || (this.Capabilities == null && other.Capabilities == null))
                && Equals(this.Authentication, other.Authentication)
                && this.Status == other.Status
                && string.Equals(this.GenerationId, other.GenerationId)
                && this.DeviceScope.Equals(other.DeviceScope)
                && ((this.ParentScopes != null && other.ParentScopes != null && Enumerable.SequenceEqual(this.ParentScopes, other.ParentScopes)) || (this.ParentScopes == null && other.ParentScopes == null));
        }
    }
}
