// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;

    class TokenHelper
    {
        public static DateTime GetTokenExpiry(string hostName, string token)
        {
            try
            {
                SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(hostName, token);
                DateTime expiryTime = sharedAccessSignature.ExpiresOn.ToUniversalTime();
                return expiryTime;
            }
            catch (UnauthorizedAccessException)
            {
                return DateTime.MinValue;
            }
        }

        public static bool IsTokenExpired(string hostName, string token)
        {
            try
            {
                SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(hostName, token);
                return sharedAccessSignature.IsExpired();
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        public static TimeSpan GetTokenExpiryTimeRemaining(string hostName, string token) => GetTokenExpiry(hostName, token) - DateTime.UtcNow;

        class SharedAccessSignature
        {
            const string SharedAccessSignatureName = "SharedAccessSignature";
            const string AudienceFieldName = "sr";
            const string SignatureFieldName = "sig";
            const string KeyNameFieldName = "skn";
            const string ExpiryFieldName = "se";
            const string KeyValueSeparator = "=";
            const string PairSeparator = "&";
            static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

            readonly string encodedAudience;
            readonly string expiry;

            public string IotHubName { get; }

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

                if (!parsedFields.TryGetValue(SignatureFieldName, out string signature))
                {
                    throw new FormatException(string.Format(CultureInfo.InvariantCulture, "Missing field: {0}", SignatureFieldName));
                }

                if (!parsedFields.TryGetValue(ExpiryFieldName, out string expiry))
                {
                    throw new FormatException(string.Format(CultureInfo.InvariantCulture, "Missing field: {0}", ExpiryFieldName));
                }

                // KeyName (skn) is optional.
                parsedFields.TryGetValue(KeyNameFieldName, out string keyName);

                if (!parsedFields.TryGetValue(AudienceFieldName, out string encodedAudience))
                {
                    throw new FormatException(string.Format(CultureInfo.InvariantCulture, "Missing field: {0}", AudienceFieldName));
                }

                return new SharedAccessSignature(iotHubName, EpochTime + TimeSpan.FromSeconds(double.Parse(expiry, CultureInfo.InvariantCulture)), expiry, keyName, signature, encodedAudience);
            }

            public static bool IsSharedAccessSignature(string rawSignature)
            {
                if (string.IsNullOrWhiteSpace(rawSignature))
                {
                    return false;
                }

                try
                {
                    IDictionary<string, string> parsedFields = ExtractFieldValues(rawSignature);
                    bool isSharedAccessSignature = parsedFields.TryGetValue(SignatureFieldName, out string signature);
                    return isSharedAccessSignature;
                }
                catch (FormatException)
                {
                    return false;
                }
            }

            public bool IsExpired()
            {
                return this.ExpiresOn + MaxClockSkew < DateTime.UtcNow;
            }

            public string ComputeSignature(byte[] key)
            {
                var fields = new List<string>
            {
                this.encodedAudience,
                this.expiry,
            };
                string value = string.Join("\n", fields);
                return Sign(key, value);
            }

            internal static string Sign(byte[] key, string value)
            {
                using (var algorithm = new HMACSHA256(key))
                {
                    return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)));
                }
            }

            SharedAccessSignature(string iotHubName, DateTime expiresOn, string expiry, string keyName, string signature, string encodedAudience)
            {
                if (string.IsNullOrWhiteSpace(iotHubName))
                {
                    throw new ArgumentNullException(nameof(iotHubName));
                }

                this.ExpiresOn = expiresOn;

                if (this.IsExpired())
                {
                    throw new UnauthorizedAccessException($"The specified SAS token is expired on {this.ExpiresOn}.");
                }

                this.IotHubName = iotHubName;
                this.Signature = signature;
                this.Audience = WebUtility.UrlDecode(encodedAudience);
                this.encodedAudience = encodedAudience;
                this.expiry = expiry;
                this.KeyName = keyName ?? string.Empty;
            }

            static IDictionary<string, string> ExtractFieldValues(string sharedAccessSignature)
            {
                string[] lines = sharedAccessSignature.Split();

                if (!string.Equals(lines[0].Trim(), SharedAccessSignatureName, StringComparison.Ordinal) || lines.Length != 2)
                {
                    throw new FormatException("Malformed signature");
                }

                IDictionary<string, string> parsedFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] fields = lines[1].Trim().Split(new string[] { PairSeparator }, StringSplitOptions.None);

                foreach (string field in fields)
                {
                    if (!string.IsNullOrEmpty(field))
                    {
                        string[] fieldParts = field.Split(new string[] { KeyValueSeparator }, StringSplitOptions.None);
                        if (string.Equals(fieldParts[0], AudienceFieldName, StringComparison.OrdinalIgnoreCase))
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
}
