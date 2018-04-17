// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper.Test
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class EdgeletSignatureProviderTest : IClassFixture<EdgeletFixture>
    {
        readonly string serverUrl;
        readonly string keyName = "module1";
        readonly string data = "data";

        public EdgeletSignatureProviderTest(EdgeletFixture edgeletFixture)
        {
            this.serverUrl = edgeletFixture.ServiceUrl;
        }

        [Fact]
        public async Task TestSignAsync_ShouldReturnSignature()
        {
            string digest;
            using (var algorithm = new HMACSHA256(Encoding.UTF8.GetBytes(this.keyName)))
            {
                digest = Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(this.data)));
            }

            var client = new EdgeletSignatureProvider(this.serverUrl);

            string signature = await client.SignAsync(this.keyName, this.data);

            Assert.Equal(digest, signature);
        }
    }
}
