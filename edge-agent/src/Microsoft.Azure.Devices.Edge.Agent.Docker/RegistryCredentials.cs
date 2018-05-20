// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class RegistryCredentials : IEquatable<RegistryCredentials>
    {
        public RegistryCredentials(string address, string username, string password)
        {
            this.Address = Preconditions.CheckNonWhiteSpace(address, nameof(address));
            this.Username = Preconditions.CheckNonWhiteSpace(username, nameof(username));
            this.Password = Preconditions.CheckNonWhiteSpace(password, nameof(password));
        }

        [JsonProperty(Required = Required.Always, PropertyName = "address")]
        public string Address { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "username")]
        public string Username { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "password")]
        public string Password { get; }

        public override bool Equals(object obj) => this.Equals(obj as RegistryCredentials);

        public bool Equals(RegistryCredentials other) =>
            this.Address == other.Address &&
            this.Username == other.Username &&
            this.Password == other.Password;

        public override int GetHashCode()
        {
            int hashCode = 217634204;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Address);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Username);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Password);
            return hashCode;
        }
    }
}
