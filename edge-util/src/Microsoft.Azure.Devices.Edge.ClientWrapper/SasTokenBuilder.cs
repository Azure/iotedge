// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Devices.Common;

    static class SasTokenBuilder
    {
        public static string BuildSasToken(string audience, string signature, string expiry, string keyName)
        {
            // Example returned string:
            // SharedAccessSignature sr=ENCODED(dh://myiothub.azure-devices.net/a/b/c?myvalue1=a)&sig=<Signature>&se=<ExpiresOnValue>[&skn=<KeyName>]

            var buffer = new StringBuilder();
            buffer.AppendFormat(CultureInfo.InvariantCulture, "{0} {1}={2}&{3}={4}&{5}={6}",
                SharedAccessSignatureConstants.SharedAccessSignature,
                SharedAccessSignatureConstants.AudienceFieldName, audience,
                SharedAccessSignatureConstants.SignatureFieldName, WebUtility.UrlEncode(signature),
                SharedAccessSignatureConstants.ExpiryFieldName, WebUtility.UrlEncode(expiry));

            if (!string.IsNullOrWhiteSpace(keyName))
            {
                buffer.AppendFormat(CultureInfo.InvariantCulture, "&{0}={1}",
                    SharedAccessSignatureConstants.KeyNameFieldName, WebUtility.UrlEncode(keyName));
            }

            return buffer.ToString();
        }

        public static string BuildExpiresOn(DateTime startTime, TimeSpan timeToLive)
        {
            DateTime expiresOn = startTime.Add(timeToLive);
            TimeSpan secondsFromBaseTime = expiresOn.Subtract(SharedAccessSignatureConstants.EpochTime);
            long seconds = Convert.ToInt64(secondsFromBaseTime.TotalSeconds, CultureInfo.InvariantCulture);
            return Convert.ToString(seconds, CultureInfo.InvariantCulture);
        }

        public static string BuildAudience(string iotHub, string deviceId, string moduleId)
        {
            string audience = WebUtility.UrlEncode("{0}/devices/{1}/modules/{2}".FormatInvariant(
                   iotHub,
                   deviceId,
                   moduleId));

            return audience;
        }
    }
}
