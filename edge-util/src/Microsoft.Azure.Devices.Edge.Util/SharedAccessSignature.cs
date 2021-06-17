// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// A shared access signature, which can be used for authorization to an IoT Hub.
    /// </summary>
    public sealed class SharedAccessSignature
    {
        private readonly string encodedAudience;
        private readonly string expiry;

        private SharedAccessSignature(
            string iotHubName,
            DateTime expiresOn,
            string expiry,
            string keyName,
            string signature,
            string encodedAudience)
        {
            if (string.IsNullOrWhiteSpace(iotHubName))
            {
                throw new ArgumentNullException(nameof(iotHubName));
            }

            this.ExpiresOn = expiresOn;

            if (this.IsExpired())
            {
                throw new UnauthorizedAccessException("The specified SAS token is expired");
            }

            this.IotHubName = iotHubName;
            this.Signature = signature;
            this.Audience = WebUtility.UrlDecode(encodedAudience);
            this.encodedAudience = encodedAudience;
            this.expiry = expiry;
            this.KeyName = keyName ?? string.Empty;
        }

        /// <summary>
        /// The IoT hub name.
        /// </summary>
        public string IotHubName { get; private set; }

        /// <summary>
        /// The date and time the SAS expires.
        /// </summary>
        public DateTime ExpiresOn { get; private set; }

        /// <summary>
        /// Name of the authorization rule.
        /// </summary>
        public string KeyName { get; private set; }

        /// <summary>
        /// The audience scope to which this signature applies.
        /// </summary>
        public string Audience { get; private set; }

        /// <summary>
        /// The value of the shared access signature.
        /// </summary>
        public string Signature { get; private set; }

        /// <summary>
        /// Parses a shared access signature string representation into a <see cref="SharedAccessSignature"/>./>
        /// </summary>
        /// <param name="iotHubName">The IoT Hub name.</param>
        /// <param name="rawToken">The string representation of the SAS token to parse.</param>
        /// <returns>The <see cref="SharedAccessSignature"/> instance that represents the passed in raw token.</returns>
        public static SharedAccessSignature Parse(string iotHubName, string rawToken)
        {
            if (string.IsNullOrWhiteSpace(iotHubName))
            {
                throw new ArgumentNullException(nameof(iotHubName));
            }

            if (string.IsNullOrWhiteSpace(rawToken))
            {
                throw new ArgumentNullException(nameof(rawToken));
            }

            IDictionary<string, string> parsedFields = ExtractFieldValues(rawToken);

            if (!parsedFields.TryGetValue(SharedAccessSignatureConstants.SignatureFieldName, out string signature))
            {
                throw new FormatException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Missing field: {0}",
                    SharedAccessSignatureConstants.SignatureFieldName));
            }

            if (!parsedFields.TryGetValue(SharedAccessSignatureConstants.ExpiryFieldName, out string expiry))
            {
                throw new FormatException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Missing field: {0}",
                    SharedAccessSignatureConstants.ExpiryFieldName));
            }

            // KeyName (skn) is optional.
            parsedFields.TryGetValue(SharedAccessSignatureConstants.KeyNameFieldName, out string keyName);

            if (!parsedFields.TryGetValue(SharedAccessSignatureConstants.AudienceFieldName, out string encodedAudience))
            {
                throw new FormatException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Missing field: {0}",
                    SharedAccessSignatureConstants.AudienceFieldName));
            }

            return new SharedAccessSignature(
                iotHubName,
                SharedAccessSignatureConstants.EpochTime + TimeSpan.FromSeconds(double.Parse(expiry, CultureInfo.InvariantCulture)),
                expiry,
                keyName,
                signature,
                encodedAudience);
        }

        /// <summary>
        /// Indicates if the token has expired.
        /// </summary>
        public bool IsExpired() => this.ExpiresOn + SharedAccessSignatureConstants.MaxClockSkew < DateTime.UtcNow;

        /// <summary>
        /// Authenticate against the IoT Hub using an authorization rule.
        /// </summary>
        /// <param name="sasAuthorizationRule">The properties that describe the keys to access the IotHub artifacts.</param>
        public void Authenticate(SharedAccessSignatureAuthorizationRule sasAuthorizationRule)
        {
            if (sasAuthorizationRule == null)
            {
                throw new ArgumentNullException(nameof(sasAuthorizationRule), "The SAS Authorization Rule cannot be null.");
            }

            if (this.IsExpired())
            {
                throw new UnauthorizedAccessException("The specified SAS token has expired.");
            }

            if (sasAuthorizationRule.PrimaryKey != null)
            {
                string primaryKeyComputedSignature = ComputeSignature(Convert.FromBase64String(sasAuthorizationRule.PrimaryKey), this.encodedAudience, this.expiry);

                if (CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(this.Signature), Encoding.UTF8.GetBytes(primaryKeyComputedSignature)))
                {
                    return;
                }
            }

            if (sasAuthorizationRule.SecondaryKey != null)
            {
                string secondaryKeyComputedSignature = ComputeSignature(Convert.FromBase64String(sasAuthorizationRule.SecondaryKey), this.encodedAudience, this.expiry);

                if (CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(this.Signature), Encoding.UTF8.GetBytes(secondaryKeyComputedSignature)))
                {
                    return;
                }
            }

            throw new UnauthorizedAccessException("The specified SAS token has an invalid signature. It does not match either the primary or secondary key.");
        }

        /// <summary>
        /// Compute the signature string using the SAS fields.
        /// </summary>
        /// <param name="key">Key used for computing the signature.</param>
        /// <returns>The string representation of the signature.</returns>
        public static string ComputeSignature(byte[] key, string encodedAudience, string expiry)
        {
            var fields = new List<string>
            {
                encodedAudience,
                expiry,
            };

            using var hmac = new HMACSHA256(key);
            string value = string.Join("\n", fields);
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static IDictionary<string, string> ExtractFieldValues(string sharedAccessSignature)
        {
            string[] lines = sharedAccessSignature.Split();

            if (!string.Equals(lines[0].Trim(), SharedAccessSignatureConstants.SharedAccessSignature, StringComparison.Ordinal) || lines.Length != 2)
            {
                throw new FormatException("Malformed signature");
            }

            IDictionary<string, string> parsedFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] fields = lines[1].Trim().Split(new string[] { SharedAccessSignatureConstants.PairSeparator }, StringSplitOptions.None);

            foreach (string field in fields)
            {
                if (!string.IsNullOrEmpty(field))
                {
                    string[] fieldParts = field.Split(new string[] { SharedAccessSignatureConstants.KeyValueSeparator }, StringSplitOptions.None);
                    if (string.Equals(fieldParts[0], SharedAccessSignatureConstants.AudienceFieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        // We need to preserve the casing of the escape characters in the audience,
                        // so defer decoding the URL until later.
                        parsedFields.Add(fieldParts[0], fieldParts[1]);
                    }
                    else
                    {
                        parsedFields.Add(fieldParts[0], WebUtility.UrlDecode(fieldParts[1]));
                    }
                }
            }

            return parsedFields;
        }
    }
}
