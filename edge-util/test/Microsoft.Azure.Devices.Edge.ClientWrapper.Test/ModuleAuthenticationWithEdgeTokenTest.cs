// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper.Test
{
    using System.Globalization;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ModuleAuthenticationWithEdgeTokenTest
    {
        string signature = "signature";
        string deviceId = "device1";
        string moduleId = "module1";
        string iotHub = "iothub.test";

        [Fact]
        public async Task TestSafeCreateNewToken_ShouldReturnSasToken()
        {
            // Arrange
            var httpClient = new Mock<ISignatureProvider>();
            httpClient.Setup(p => p.SignAsync(this.moduleId, It.IsAny<string>())).Returns(Task.FromResult(this.signature));

            var moduleAuthenticationWithEdgelet = new ModuleAuthenticationWithEdgeToken(httpClient.Object, this.deviceId, this.moduleId);

            // Act
            string sasToken = await moduleAuthenticationWithEdgelet.GetTokenAsync(this.iotHub);
            SasToken token = SasToken.Parse(sasToken);

            string audience = WebUtility.UrlEncode(string.Format(CultureInfo.InvariantCulture, "{0}/devices/{1}/modules/{2}",
                    this.iotHub,
                    this.deviceId,
                    this.moduleId));

            // Assert
            httpClient.Verify();
            Assert.NotNull(sasToken);
            Assert.Equal(this.signature, token.Signature);
            Assert.Equal(audience, token.Audience);
            Assert.Equal(this.moduleId, token.KeyName);
        }

        [Fact]
        public async Task TestSafeCreateNewToken_WhenEdgeletThrows_ShouldThrow()
        {
            var httpClient = new Mock<ISignatureProvider>();
            httpClient.Setup(p => p.SignAsync(this.moduleId, It.IsAny<string>())).Throws(new EdgeletCommunicationException());

            var moduleAuthenticationWithEdgelet = new ModuleAuthenticationWithEdgeToken(httpClient.Object, this.deviceId, this.moduleId);

            await Assert.ThrowsAsync<EdgeletCommunicationException>(async () => await moduleAuthenticationWithEdgelet.GetTokenAsync(this.iotHub));
        }
    }
}
