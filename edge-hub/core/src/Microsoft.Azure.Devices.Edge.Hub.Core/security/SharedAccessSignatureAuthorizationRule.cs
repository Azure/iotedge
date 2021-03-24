// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using Newtonsoft.Json;

    public sealed class SharedAccessSignatureAuthorizationRule : IEquatable<SharedAccessSignatureAuthorizationRule>
    {
        string primaryKey;
        string secondaryKey;

        [JsonProperty(PropertyName = "keyName")]
        public string KeyName { get; set; }

        [JsonProperty(PropertyName = "primaryKey")]
        public string PrimaryKey
        {
            get => this.primaryKey;

            set
            {
                StringValidationHelper.EnsureNullOrBase64String(value, "PrimaryKey");
                this.primaryKey = value;
            }
        }

        [JsonProperty(PropertyName = "secondaryKey")]
        public string SecondaryKey
        {
            get => this.secondaryKey;

            set
            {
                StringValidationHelper.EnsureNullOrBase64String(value, "SecondaryKey");
                this.secondaryKey = value;
            }
        }

        [JsonProperty(PropertyName = "rights")]
        public AccessRights Rights { get; set; }

        public bool Equals(SharedAccessSignatureAuthorizationRule other)
        {
            if (other == null)
            {
                return false;
            }

            bool equals = string.Equals(this.KeyName, other.KeyName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(this.PrimaryKey, other.PrimaryKey, StringComparison.Ordinal) &&
                string.Equals(this.SecondaryKey, other.SecondaryKey, StringComparison.Ordinal) &&
                Equals(this.Rights, other.Rights);

            return equals;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as SharedAccessSignatureAuthorizationRule);
        }

        public static int GetHashCode(SharedAccessSignatureAuthorizationRule rule)
        {
            if (rule == null)
            {
                return 0;
            }

            int hashKeyName, hashPrimaryKey, hashSecondaryKey, hashRights;

            hashKeyName = rule.KeyName == null ? 0 : rule.KeyName.GetHashCode(StringComparison.InvariantCultureIgnoreCase);
            hashPrimaryKey = rule.PrimaryKey == null ? 0 : rule.PrimaryKey.GetHashCode(StringComparison.InvariantCultureIgnoreCase);
            hashSecondaryKey = rule.SecondaryKey == null ? 0 : rule.SecondaryKey.GetHashCode(StringComparison.InvariantCultureIgnoreCase);
            hashRights = rule.Rights.GetHashCode();

            return hashKeyName ^ hashPrimaryKey ^ hashSecondaryKey ^ hashRights;
        }

        public override int GetHashCode()
        {
            return GetHashCode(this);
        }
    }
}
