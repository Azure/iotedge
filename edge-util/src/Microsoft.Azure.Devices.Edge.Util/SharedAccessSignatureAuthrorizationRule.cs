// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// A shared access signature based authorization rule for authenticating requests against an IoT Hub.
    /// </summary>
    public sealed class SharedAccessSignatureAuthorizationRule : IEquatable<SharedAccessSignatureAuthorizationRule>
    {
        private string primaryKey;
        private string secondaryKey;

        [JsonProperty(PropertyName = "keyName")]
        public string KeyName { get; set; }

        /// <summary>
        /// The primary key associated with the shared access policy.
        /// </summary>
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

        /// <summary>
        /// The secondary key associated with the shared access policy.
        /// </summary>
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

        /// <summary>
        /// The name of the shared access policy that will be used to grant permission to IoT Hub endpoints.
        /// </summary>
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

        /// <inheritdoc/>
        public override bool Equals(object obj) => this.Equals(obj as SharedAccessSignatureAuthorizationRule);

        /// <summary>
        /// Gets a hash code for a given object.
        /// </summary>
        /// <returns>A hash code for the given object.</returns>
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

        /// <summary>
        /// Gets a hash code for the current object.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode() => GetHashCode(this);

        /// <summary>
        /// Shared access policy permissions of IoT hub.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-security#iot-hub-permissions"/>.
        /// </summary>
        [Flags]
        [JsonConverter(typeof(StringEnumConverter))]
        public enum AccessRights
        {
            /// <summary>
            /// Grants read access to the identity registry.
            /// Identity registry stores information about the devices and modules permitted to connect to the IoT hub.
            /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-identity-registry"/>.
            /// </summary>
            RegistryRead = 1,

            /// <summary>
            /// Grants read and write access to the identity registry.
            /// Identity registry stores information about the devices and modules permitted to connect to the IoT hub.
            /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-identity-registry"/>.
            /// </summary>
            RegistryWrite = RegistryRead | 2,

            /// <summary>
            /// Grants access to service facing communication and monitoring endpoints.
            /// It grants permission to receive device-to-cloud messages, send cloud-to-device messages, retrieve delivery acknowledgments for sent messages
            /// and file uploads, retrieve desired and reported properties on device twins, update tags and desired properties on device twins,
            /// and run queries.
            /// </summary>
            ServiceConnect = 4,

            /// <summary>
            /// Grants access to device facing endpoints.
            /// It grants permission to send device-to-cloud messages, receive cloud-to-device messages, perform file upload from a device, receive device twin
            /// desired property notifications and update device twin reported properties.
            /// </summary>
            DeviceConnect = 8
        }
    }
}