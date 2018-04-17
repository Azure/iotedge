// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper.Test
{
    using System;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class SasTokenBuilderTest
    {
        [Fact]
        public void TestBuildExpiry_ShouldReturnExpireOn()
        {
            var startTime = new DateTime(2018, 1, 1);
            TimeSpan timeToLive = TimeSpan.FromMinutes(60);
            long seconds = 1514768400;
            string expiresOn = SasTokenBuilder.BuildExpiresOn(startTime, timeToLive);

            Assert.Equal(seconds.ToString(), expiresOn);
        }

        [Fact]
        public void TestBuildAudience_ShouldReturnSasToken()
        {
            string audience = WebUtility.UrlEncode("iothub.test/devices/device1/modules/module1");
            string deviceId = "device1";
            string iotHub = "iothub.test";
            string moduleId = "module1";
            string builtAudience = SasTokenBuilder.BuildAudience(iotHub, deviceId, moduleId);

            Assert.Equal(audience, builtAudience);
        }

        [Fact]
        public void TestBuildSasToken_ShouldReturnSasToken()
        {
            string audience = "iothub.test/devices/device1/modules/module1";
            string signature = "signature";
            string expiry = "1514768400";
            string keyname = "module1";
            string sasTokenString = SasTokenBuilder.BuildSasToken(audience, signature, expiry, keyname);

            SasToken token = SasToken.Parse(sasTokenString);

            Assert.Equal(audience, token.Audience);
            Assert.Equal(signature, token.Signature);
            Assert.Equal(keyname, token.KeyName);
            Assert.Equal(expiry, token.ExpireOn);
        }
    }
}
