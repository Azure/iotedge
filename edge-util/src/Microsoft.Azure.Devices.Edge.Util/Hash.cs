// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    public static class Hash
    {
        static readonly ThreadLocal<SHA256> Hasher = new ThreadLocal<SHA256>(() => SHA256.Create());

        public static string CreateSha256(this string input)
        {
            byte[] hash = Hasher.Value.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hash);
        }
    }
}
