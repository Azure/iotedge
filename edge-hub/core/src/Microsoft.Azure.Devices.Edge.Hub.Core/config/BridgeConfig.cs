// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Domain object that represents Bridge configuration for MQTT Broker.
    ///
    /// This object is being constructed from the EdgeHub twin's desired properties.
    /// See <see cref="EdgeHubDesiredProperties"/> for DTO.
    /// </summary>
    public class BridgeConfig : List<Bridge>, IEquatable<BridgeConfig>
    {
        public bool Equals(BridgeConfig other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Enumerable.SequenceEqual(this, other);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as BridgeConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = this.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode()));
                return hash;
            }
        }
    }

    public class Bridge : IEquatable<Bridge>
    {
        [JsonConstructor]
        public Bridge(string endpoint, IList<Settings> settings)
        {
            this.Endpoint = endpoint;
            this.Settings = settings ?? new List<Settings>();
        }

        [JsonProperty("endpoint", Required = Required.Always)]
        public string Endpoint { get; }

        [JsonProperty("settings", Required = Required.Always)]
        public IList<Settings> Settings { get; }

        public bool Equals(Bridge other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Endpoint.Equals(other.Endpoint)
                && Enumerable.SequenceEqual(this.Settings, other.Settings);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as Bridge);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Endpoint?.GetHashCode() ?? 0;
                hashCode = this.Settings.Aggregate(hashCode, (acc, item) => (acc * 31 + item.GetHashCode()));
                return hashCode;
            }
        }
    }

    public class Settings : IEquatable<Settings>
    {
        [JsonConstructor]
        public Settings(
            Direction direction,
            string topic,
            string inPrefix,
            string outPrefix)
        {
            this.Direction = direction;
            this.Topic = topic;
            this.InPrefix = inPrefix ?? string.Empty;
            this.OutPrefix = outPrefix ?? string.Empty;
        }

        [JsonProperty("direction", Required = Required.Always)]
        public Direction Direction { get; }

        [JsonProperty("topic", Required = Required.Always)]
        public string Topic { get; }

        [JsonProperty("inPrefix")]
        public string InPrefix { get; }

        [JsonProperty("outPrefix")]
        public string OutPrefix { get; }

        public bool Equals(Settings other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Direction.Equals(other.Direction)
                && this.Topic.Equals(other.Topic)
                && this.InPrefix.Equals(other.InPrefix)
                && this.OutPrefix.Equals(other.OutPrefix);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as Settings);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Direction.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.Topic?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.InPrefix?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.OutPrefix?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum Direction
    {
        [EnumMember(Value = "in")]
        In,
        [EnumMember(Value = "out")]
        Out,
        [EnumMember(Value = "both")]
        Both
    }
}
