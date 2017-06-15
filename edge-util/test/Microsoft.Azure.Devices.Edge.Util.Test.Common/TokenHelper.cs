// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web;

    public static class TokenHelper
    {
        public static string CreateSasToken(string resourceUri, string key = null, bool expired = false)
        {
            key = key ?? GetRandomKey();            
            TimeSpan sinceEpoch = (expired ? new DateTime(2010, 1, 1) : new DateTime(2020, 1, 1)) - new DateTime(1970, 1, 1);
            string expiry = Convert.ToString((int)sinceEpoch.TotalSeconds);
            string stringToSign = HttpUtility.UrlEncode(resourceUri) + "\n" + expiry;
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            string sasToken = $"SharedAccessSignature sr={HttpUtility.UrlEncode(resourceUri)}&sig={HttpUtility.UrlEncode(signature)}&se={expiry}";
            return sasToken;
        }

        static string GetRandomKey(int length = 45)
        {
            var rand = new Random();
            var sb = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                sb.Append('a' + rand.Next(0, 25));
            }
            return sb.ToString();
        }
    }
}