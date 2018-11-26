// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Xunit;

    using TokenHelper = Microsoft.Azure.Devices.Edge.Hub.CloudProxy.TokenHelper;

    [Unit]
    public class TokenHelperTest
    {
        [Fact]
        public void CheckTokenExpiredTest()
        {
            // Arrange
            string hostname = "dummy.azure-devices.net";
            DateTime expiryTime = DateTime.UtcNow.AddHours(2);
            string validToken = Util.Test.Common.TokenHelper.CreateSasToken(hostname, expiryTime);

            // Act
            DateTime actualExpiryTime = TokenHelper.GetTokenExpiry(hostname, validToken);

            // Assert
            Assert.True(actualExpiryTime - expiryTime < TimeSpan.FromSeconds(1));

            // Arrange
            expiryTime = DateTime.UtcNow.AddHours(-2);
            string expiredToken = Util.Test.Common.TokenHelper.CreateSasToken(hostname, expiryTime);

            // Act
            actualExpiryTime = TokenHelper.GetTokenExpiry(hostname, expiredToken);

            // Assert
            Assert.Equal(DateTime.MinValue, actualExpiryTime);
        }

        [Fact]
        public void GetIsTokenExpiredTest()
        {
            // Arrange
            DateTime tokenExpiry = DateTime.UtcNow.AddYears(1);
            string token = Util.Test.Common.TokenHelper.CreateSasToken("azure.devices.net", tokenExpiry);

            // Act
            TimeSpan expiryTimeRemaining = TokenHelper.GetTokenExpiryTimeRemaining("azure.devices.net", token);

            // Assert
            Assert.True(expiryTimeRemaining - (tokenExpiry - DateTime.UtcNow) < TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void GetTokenExpiryBufferSecondsTest()
        {
            string token = Util.Test.Common.TokenHelper.CreateSasToken("azure.devices.net");
            TimeSpan timeRemaining = TokenHelper.GetTokenExpiryTimeRemaining("foo.azuredevices.net", token);
            Assert.True(timeRemaining > TimeSpan.Zero);
        }
    }
}
