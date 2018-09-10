// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class SharedAccessKeySignatureProviderTest
    {
        [Fact]
        [Unit]
        public async Task SignTest()
        {
            // Arrange
            string audience = SasTokenHelper.BuildAudience("foo.azure-devices.net", "ed1", "$edgeHub");
            string expiresOn = SasTokenHelper.BuildExpiresOn(new DateTime(2018, 01, 01), TimeSpan.FromHours(1));
            string data = string.Join("\n", new List<string> { audience, expiresOn });
            string key = Convert.ToBase64String(Encoding.UTF8.GetBytes("DummyKey123"));
            string expectedToken = Convert.ToBase64String(new HMACSHA256(Convert.FromBase64String(key)).ComputeHash(Encoding.UTF8.GetBytes(data)));
            var signatureProvider = new SharedAccessKeySignatureProvider(key);

            // Act
            string token = await signatureProvider.SignAsync(data);

            // Assert
            Assert.Equal(expectedToken, token);
        }
    }
}
