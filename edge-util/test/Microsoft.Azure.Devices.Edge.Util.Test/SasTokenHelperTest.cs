// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class SasTokenHelperTest
    {
        [Fact]
        public void BuildAudienceModuleTest()
        {
            // Arrange
            string iotHubHostName = "testIotHub.azure-devices.net";
            string deviceId = "device1";
            string moduleId = "module1";

            // Act
            string audience = SasTokenHelper.BuildAudience(iotHubHostName, deviceId, moduleId);

            // Assert
            Assert.Equal("testIotHub.azure-devices.net%2Fdevices%2Fdevice1%2Fmodules%2Fmodule1", audience);
        }

        [Fact]
        public void BuildAudienceDeviceTest()
        {
            // Arrange
            string iotHubHostName = "testIotHub.azure-devices.net";
            string deviceId = "device1";

            // Act
            string audience = SasTokenHelper.BuildAudience(iotHubHostName, deviceId);

            // Assert
            Assert.Equal("testIotHub.azure-devices.net%2Fdevices%2Fdevice1", audience);
        }

        [Fact]
        public void BuildAudienceModule_WithSplChars_Test()
        {
            // Arrange
            string iotHubHostName = "testIotHub.azure-devices.net";
            string deviceId = "n@m.et#st";
            string moduleId = "$edgeAgent";

            // Act
            string audience = SasTokenHelper.BuildAudience(iotHubHostName, deviceId, moduleId);

            // Assert
            Assert.Equal("testIotHub.azure-devices.net%2Fdevices%2Fn%2540m.et%2523st%2Fmodules%2F%2524edgeAgent", audience);
        }

        [Fact]
        public void BuildAudienceDevice_WithSplChars_Test()
        {
            // Arrange
            string iotHubHostName = "testIotHub.azure-devices.net";
            string deviceId = "n@m.et#st";

            // Act
            string audience = SasTokenHelper.BuildAudience(iotHubHostName, deviceId);

            // Assert
            Assert.Equal("testIotHub.azure-devices.net%2Fdevices%2Fn%2540m.et%2523st", audience);
        }

        [Fact]
        public void BuildExpiresOnTest()
        {
            // Arrange
            var startTime = new DateTime(2019, 01, 01);
            TimeSpan ttl = TimeSpan.FromHours(1);

            // Act
            string expiresOn = SasTokenHelper.BuildExpiresOn(startTime, ttl);

            // Assert
            Assert.Equal("1546304400", expiresOn);
        }

        [Fact]
        public void BuildSasTokenTest()
        {
            // Arrange
            string audience = "testIotHub.azure-devices.net%2Fdevices%2Fdevice1%2Fmodules%2Fmodule1";
            string signature = Guid.NewGuid().ToString();
            string expiry = "1546304400";
            string expectedToken = $"SharedAccessSignature sr=testIotHub.azure-devices.net%2Fdevices%2Fdevice1%2Fmodules%2Fmodule1&sig={signature}&se=1546304400";

            // Act
            string sasToken = SasTokenHelper.BuildSasToken(audience, signature, expiry);

            // Assert
            Assert.Equal(expectedToken, sasToken);
        }
    }
}
