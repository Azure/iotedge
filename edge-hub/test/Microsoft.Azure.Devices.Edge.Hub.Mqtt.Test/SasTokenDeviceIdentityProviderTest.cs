// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Xunit;

    public class SasTokenDeviceIdentityProviderTest
    {
        [Fact]
        public void ParseUserNameTest()
        {
            string username1 = "iotHub1/device1/api-version=2010-01-01&DeviceClientType=customDeviceClient1";
            (string deviceId1, string moduleId1, string deviceClientType1, bool isModuleIdentity1) = SasTokenDeviceIdentityProvider.ParseUserName(username1);
            Assert.Equal("device1", deviceId1);
            Assert.Equal(string.Empty, moduleId1);
            Assert.Equal("customDeviceClient1", deviceClientType1);
            Assert.False(isModuleIdentity1);

            string username2 = "iotHub1/device1/module1/api-version=2010-01-01&DeviceClientType=customDeviceClient2";
            (string deviceId2, string moduleId2, string deviceClientType2, bool isModuleIdentity2) = SasTokenDeviceIdentityProvider.ParseUserName(username2);
            Assert.Equal("device1", deviceId2);
            Assert.Equal("module1", moduleId2);
            Assert.Equal("customDeviceClient2", deviceClientType2);
            Assert.True(isModuleIdentity2);

            string username3 = "iotHub1/device1/module1/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003";
            (string deviceId3, string moduleId3, string deviceClientType3, bool isModuleIdentity3) = SasTokenDeviceIdentityProvider.ParseUserName(username3);
            Assert.Equal("device1", deviceId3);
            Assert.Equal("module1", moduleId3);
            Assert.Equal("Microsoft.Azure.Devices.Client/1.5.1-preview-003", deviceClientType3);
            Assert.True(isModuleIdentity3);
        }

        [Theory]
        [InlineData("iotHub1/device1")]
        [InlineData("iotHub1/device1/fooBar")]
        [InlineData("iotHub1/device1/api-version")]
        [InlineData("iotHub1/device1/module1/fooBar")]
        [InlineData("iotHub1/device1/module1/api-version")]
        [InlineData("iotHub1/device1/module1/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003/prodInfo")]
        [InlineData("iotHub1/device1/module1/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client")]
        [InlineData("iotHub1/device1/module1")]
        public void ParseUserNameErrorTest(string username)
        {
            Assert.Throws<EdgeHubConnectionException>(() => SasTokenDeviceIdentityProvider.ParseUserName(username));
        }
    }
}
