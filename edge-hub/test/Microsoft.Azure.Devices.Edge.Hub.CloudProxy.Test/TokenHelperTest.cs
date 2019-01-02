// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class TokenHelperTest
    {
        [Fact]
        public void CheckTokenExpiredTest()
        {
            // Arrange
            string hostname = "dummy.azure-devices.net";
            DateTime expiryTime = DateTime.UtcNow.AddHours(2);
            string validToken = TokenHelper.CreateSasToken(hostname, expiryTime);

            // Act
            DateTime actualExpiryTime = Hub.CloudProxy.TokenHelper.GetTokenExpiry(hostname, validToken);

            // Assert
            Assert.True(actualExpiryTime - expiryTime < TimeSpan.FromSeconds(1));

            // Arrange
            expiryTime = DateTime.UtcNow.AddHours(-2);
            string expiredToken = TokenHelper.CreateSasToken(hostname, expiryTime);

            // Act
            actualExpiryTime = Hub.CloudProxy.TokenHelper.GetTokenExpiry(hostname, expiredToken);

            // Assert
            Assert.Equal(DateTime.MinValue, actualExpiryTime);
        }

        [Fact]
        public void GetIsTokenExpiredTest()
        {
            // Arrange
            DateTime tokenExpiry = DateTime.UtcNow.AddYears(1);
            string token = TokenHelper.CreateSasToken("azure.devices.net", tokenExpiry);

            // Act
            TimeSpan expiryTimeRemaining = Hub.CloudProxy.TokenHelper.GetTokenExpiryTimeRemaining("azure.devices.net", token);

            // Assert
            Assert.True(expiryTimeRemaining - (tokenExpiry - DateTime.UtcNow) < TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void GetTokenExpiryBufferSecondsTest()
        {
            string token = TokenHelper.CreateSasToken("azure.devices.net");
            TimeSpan timeRemaining = Hub.CloudProxy.TokenHelper.GetTokenExpiryTimeRemaining("foo.azuredevices.net", token);
            Assert.True(timeRemaining > TimeSpan.Zero);
        }
    }
}
