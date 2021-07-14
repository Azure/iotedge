// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
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
        public async Task GetModuleTokenTestCacheTest()
        {
            // Arrange
            var signatureProvider = new Mock<ISignatureProvider>();
            signatureProvider.Setup(s => s.SignAsync(It.IsAny<string>())).Returns(Task.FromResult(Guid.NewGuid().ToString()));

            string iotHubHostName = "testIoThub";
            string deviceId = "testDeviceId";
            string moduleId = "$edgeHub";
            ITokenProvider tokenProvider = new ClientTokenProvider(signatureProvider.Object, iotHubHostName, deviceId, moduleId, TimeSpan.FromSeconds(30));

            // Act
            string token = await tokenProvider.GetTokenAsync(Option.None<TimeSpan>());

            // Assert
            Assert.NotNull(token);
            signatureProvider.Verify(s => s.SignAsync(It.IsAny<string>()), Times.Once);

            // Act
            var tasks = new List<Task<string>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(tokenProvider.GetTokenAsync(Option.None<TimeSpan>()));
            }

            string[] tokens = await Task.WhenAll(tasks);

            // Assert
            Assert.NotNull(tokens);
            Assert.Equal(5, tokens.Length);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(token, tokens[i]);
            }

            signatureProvider.Verify(s => s.SignAsync(It.IsAny<string>()), Times.Once);

            await Task.Delay(TimeSpan.FromSeconds(15));

            // Act
            string newToken = await tokenProvider.GetTokenAsync(Option.None<TimeSpan>());

            // Assert
            Assert.NotNull(newToken);
            Assert.NotEqual(token, newToken);
            signatureProvider.Verify(s => s.SignAsync(It.IsAny<string>()), Times.AtMost(2));
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
