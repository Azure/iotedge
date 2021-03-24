// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;

    public sealed class SharedAccessSignature : ISharedAccessSignatureCredential
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

        public string IotHubName { get; private set; }
        public DateTime ExpiresOn { get; private set; }
        public string KeyName { get; private set; }
        public string Audience { get; private set; }
        public string Signature { get; private set; }

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
        /// Validates whether a string token is a valid SAS token.
        /// </summary>
        /// <param name="rawSignature">The string representation of the SAS token to parse.</param>
        /// <returns>True if the passed in raw signature is a valid SAS token. False otherwise.</returns>
        public static bool IsSharedAccessSignature(string rawSignature)
        {
            if (string.IsNullOrWhiteSpace(rawSignature))
            {
                return false;
            }

            IDictionary<string, string> parsedFields = ExtractFieldValues(rawSignature);
            bool isSharedAccessSignature = parsedFields.TryGetValue(SharedAccessSignatureConstants.SignatureFieldName, out _);

            return isSharedAccessSignature;
        }

        public bool IsExpired()
        {
            return this.ExpiresOn + SharedAccessSignatureConstants.MaxClockSkew < DateTime.UtcNow;
        }

        public DateTime ExpiryTime()
        {
            return this.ExpiresOn + SharedAccessSignatureConstants.MaxClockSkew;
        }

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
                string primareyKeyComputedSignature = this.ComputeSignature(Convert.FromBase64String(sasAuthorizationRule.PrimaryKey));
                if (StringComparer.Ordinal.Equals(this.Signature, primareyKeyComputedSignature))
                {
                    return;
                }
            }

            if (sasAuthorizationRule.SecondaryKey != null)
            {
                string secondaryKeyComputedSignature = this.ComputeSignature(Convert.FromBase64String(sasAuthorizationRule.SecondaryKey));
                if (StringComparer.Ordinal.Equals(this.Signature, secondaryKeyComputedSignature))
                {
                    return;
                }
            }

            throw new UnauthorizedAccessException("The specified SAS token has an invalid signature. It does not match either the primary or secondary key.");
        }

        /// <summary>
        /// Authorize to the IoT Hub.
        /// </summary>
        /// <param name="iotHubHostName">IoT Hub host to authorize against.</param>
        public void Authorize(string iotHubHostName)
        {
            if (string.IsNullOrWhiteSpace(iotHubHostName))
            {
                throw new ArgumentNullException(nameof(iotHubHostName));
            }

            if (string.IsNullOrWhiteSpace(this.IotHubName))
            {
                throw new ArgumentNullException(nameof(this.IotHubName));
            }

            if (!iotHubHostName.StartsWith(string.Format(CultureInfo.InvariantCulture, "{0}.", this.IotHubName), StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("IOT hub does not correspond to host name");
            }
        }

        /// <summary>
        /// Authorize access to the provided target address.
        /// </summary>
        /// <param name="targetAddress">Target address to authorize against.</param>
        public void Authorize(Uri targetAddress)
        {
            if (targetAddress == null)
            {
                throw new ArgumentNullException(nameof(targetAddress));
            }

            string target = targetAddress.Host + targetAddress.AbsolutePath;

            if (!target.StartsWith(this.Audience.TrimEnd(new char[] { '/' }), StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Invalid target audience");
            }
        }

        /// <summary>
        /// Compute the signature string using the SAS fields.
        /// </summary>
        /// <param name="key">Key used for computing the signature.</param>
        /// <returns>The string representation of the signature.</returns>
        public string ComputeSignature(byte[] key)
        {
            var fields = new List<string>
            {
                this.encodedAudience,
                this.expiry,
            };

            using var hmac = new HMACSHA256(key);
            string value = string.Join("\n", fields);
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static IDictionary<string, string> ExtractFieldValues(string sharedAccessSignature)
        {
            string[] lines = sharedAccessSignature.Split();

            if (!StringComparer.Ordinal.Equals(
                    lines[0].Trim(),
                    SharedAccessSignatureConstants.SharedAccessSignature)
                || lines.Length != 2)
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
