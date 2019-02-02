// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Text;
    using static System.FormattableString;

    public static class SasTokenHelper
    {
        const string SharedAccessSignature = "SharedAccessSignature";
        const string AudienceFieldName = "sr";
        const string SignatureFieldName = "sig";
        const string ExpiryFieldName = "se";
        static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static string BuildSasToken(string audience, string signature, string expiry)
        {
            // Example returned string:
            // SharedAccessSignature sr=ENCODED(dh://myiothub.azure-devices.net/a/b/c?myvalue1=a)&sig=<Signature>&se=<ExpiresOnValue>[&skn=<KeyName>]
            var buffer = new StringBuilder();
            buffer.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1}={2}&{3}={4}&{5}={6}",
                SharedAccessSignature,
                AudienceFieldName,
                audience,
                SignatureFieldName,
                WebUtility.UrlEncode(signature),
                ExpiryFieldName,
                WebUtility.UrlEncode(expiry));

            return buffer.ToString();
        }

        public static string BuildExpiresOn(DateTime startTime, TimeSpan timeToLive)
        {
            DateTime expiresOn = startTime.Add(timeToLive);
            TimeSpan secondsFromBaseTime = expiresOn.Subtract(EpochTime);
            long seconds = Convert.ToInt64(secondsFromBaseTime.TotalSeconds, CultureInfo.InvariantCulture);
            return Convert.ToString(seconds, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Builds the audience from iothub deviceId and moduleId.
        /// Note that deviceId and moduleId need to be double encoded.
        /// </summary>
        public static string BuildAudience(string iotHub, string deviceId, string moduleId) =>
            WebUtility.UrlEncode(Invariant($"{iotHub}/devices/{WebUtility.UrlEncode(deviceId)}/modules/{WebUtility.UrlEncode(moduleId)}"));

        /// <summary>
        /// Builds the audience from iothub and deviceId.
        /// Note that deviceId and moduleId need to be double encoded.
        /// </summary>
        public static string BuildAudience(string iotHub, string deviceId) =>
            WebUtility.UrlEncode(Invariant($"{iotHub}/devices/{WebUtility.UrlEncode(deviceId)}"));
    }
}
