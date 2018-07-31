// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;

    public class SharedAccessKeySignatureProvider : ISignatureProvider
    {
        readonly string sasKey;

        public SharedAccessKeySignatureProvider(string sasKey)
        {
            this.sasKey = Preconditions.CheckNonWhiteSpace(sasKey, nameof(sasKey));
        }

        public Task<string> SignAsync(string data)
        {
            string token = Sign(data, this.sasKey);
            return Task.FromResult(token);
        }

        static string Sign(string requestString, string key)
        {
            using (var algorithm = new HMACSHA256(Convert.FromBase64String(key)))
            {
                return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(requestString)));
            }
        }
    }
}
