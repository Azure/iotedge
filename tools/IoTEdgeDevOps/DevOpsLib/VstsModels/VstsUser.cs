// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    // Schema reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/graph/users/list?view=azure-devops-rest-6.0
    [JsonConverter(typeof(JsonPathConverter))]
    public class VstsUser : IEquatable<VstsUser>
    {
        [JsonProperty("mailAddress")]
        public string MailAddress { get; set; }

        [JsonProperty("displayName")]
        public string Name { get; set; }

        public bool Equals(VstsUser other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.MailAddress == other.MailAddress;
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

            return this.Equals((VstsUser)obj);
        }

        public override int GetHashCode() => this.MailAddress.GetHashCode();
    }
}

