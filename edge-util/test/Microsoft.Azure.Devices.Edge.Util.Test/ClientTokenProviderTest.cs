// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class ClientTokenProviderTest
    {
        [Fact]
        [Unit]
        public async Task GetModuleTokenTest()
        {
            // Arrange
            ISignatureProvider signatureProvider = new TestSignatureProvider();
            string iotHubHostName = "testIoThub";
            string deviceId = "testDeviceId";
            string moduleId = "$edgeHub";
            ITokenProvider tokenProvider = new ClientTokenProvider(signatureProvider, iotHubHostName, deviceId, moduleId, TimeSpan.FromHours(1));
            var keys = new[] { "sr", "sig", "se" };

            // Act
            string token = await tokenProvider.GetTokenAsync(Option.None<TimeSpan>());

            // Assert
            Assert.NotNull(token);
            string[] lines = token.Split();
            Assert.Equal(2, lines.Length);
            Assert.Equal("SharedAccessSignature", lines[0]);

            string[] parts = lines[1].Split('&');
            Assert.Equal(3, parts.Length);

            foreach (string part in parts)
            {
                string[] kvp = part.Split("=");
                Assert.Contains(kvp[0], keys);
                Assert.NotNull(kvp[1]);
            }
        }

        [Fact]
        [Unit]
        public async Task GetDeviceTokenTest()
        {
            // Arrange
            ISignatureProvider signatureProvider = new TestSignatureProvider();
            string iotHubHostName = "testIoThub";
            string deviceId = "testDeviceId";
            ITokenProvider tokenProvider = new ClientTokenProvider(signatureProvider, iotHubHostName, deviceId, TimeSpan.FromHours(1));
            var keys = new[] { "sr", "sig", "se" };

            // Act
            string token = await tokenProvider.GetTokenAsync(Option.None<TimeSpan>());

            // Assert
            Assert.NotNull(token);
            string[] lines = token.Split();
            Assert.Equal(2, lines.Length);
            Assert.Equal("SharedAccessSignature", lines[0]);

            string[] parts = lines[1].Split('&');
            Assert.Equal(3, parts.Length);

            foreach (string part in parts)
            {
                string[] kvp = part.Split("=");
                Assert.Contains(kvp[0], keys);
                Assert.NotNull(kvp[1]);
            }
        }

        class TestSignatureProvider : ISignatureProvider
        {
            public Task<string> SignAsync(string data) => Task.FromResult(Convert.ToBase64String(Encoding.UTF8.GetBytes(data)));
        }
    }
}
