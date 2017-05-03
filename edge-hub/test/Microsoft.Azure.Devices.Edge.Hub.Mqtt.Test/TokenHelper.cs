// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web;

    public static class TokenHelper
    {
        public static string CreateSasToken(string resourceUri, string key)
        {
            TimeSpan sinceEpoch = new DateTime(2020, 1, 1) - new DateTime(1970, 1, 1);
            string expiry = Convert.ToString((int)sinceEpoch.TotalSeconds);
            string stringToSign = HttpUtility.UrlEncode(resourceUri) + "\n" + expiry;
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            string sasToken = $"SharedAccessSignature sr={HttpUtility.UrlEncode(resourceUri)}&sig={HttpUtility.UrlEncode(signature)}&se={expiry}";
            return sasToken;
        }
    }
}