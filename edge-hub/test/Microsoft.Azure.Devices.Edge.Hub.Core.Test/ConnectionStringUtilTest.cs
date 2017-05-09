// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class ConnectionStringUtilTest
    {
        [Fact]
        [Unit]
        public void GetDeviceIdFromConnectionStringTest()
        {
            string randomKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            string deviceString = "HostName=your-iot-hub.azure-devices.net;DeviceId=your-device;SharedAccessKey=" + randomKey;
            Assert.Equal("your-device", ConnectionStringUtil.GetDeviceIdFromConnectionString(deviceString));
        }

        [Fact]
        [Unit]
        public void GetDeviceIdFromConnectionString_AlternativeDeviceStringTest()
        {
            const string DeviceString = "This is a great, amazing device string. Believe me, it's the best device string ever!";
            Assert.Throws<FormatException>(() => ConnectionStringUtil.GetDeviceIdFromConnectionString(DeviceString));
        }

        [Fact]
        [Unit]
        public void GetDeviceIdFromConnectionString_NullOrWhitespaceTest()
        {
            string deviceString = string.Empty;
            Assert.Throws<ArgumentException>(() => ConnectionStringUtil.GetDeviceIdFromConnectionString(deviceString));
        }
    }
}
