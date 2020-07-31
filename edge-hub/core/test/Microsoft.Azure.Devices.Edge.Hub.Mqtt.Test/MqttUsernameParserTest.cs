// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class MqttUsernameParserTest
    {
        public static IEnumerable<object[]> GetUsernames()
        {
            yield return new object[] { "iotHub1/device1/api-version=2010-01-01&DeviceClientType=customDeviceClient1", "device1", string.Empty, "customDeviceClient1", string.Empty };

            yield return new object[] { "iotHub1/device1/module1/api-version=2010-01-01&DeviceClientType=customDeviceClient2", "device1", "module1", "customDeviceClient2", string.Empty };

            yield return new object[] { "iotHub1/device1/module1/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003", "device1", "module1", "Microsoft.Azure.Devices.Client/1.5.1-preview-003", string.Empty };

            yield return new object[] { "iotHub1/device1/?api-version=2010-01-01&DeviceClientType=customDeviceClient1", "device1", string.Empty, "customDeviceClient1", string.Empty };

            yield return new object[] { "iotHub1/device1/?api-version=2010-01-01&DeviceClientType=customDeviceClient1&model-id=testModelId", "device1", string.Empty, "customDeviceClient1", "testModelId" };

            yield return new object[] { "iotHub1/device1/module1/?api-version=2010-01-01&DeviceClientType=customDeviceClient1", "device1", "module1", "customDeviceClient1", string.Empty };

            yield return new object[] { "iotHub1/device1/module1/?api-version=2010-01-01&DeviceClientType=customDeviceClient1&model-id=testModelId", "device1", "module1", "customDeviceClient1", "testModelId" };

            yield return new object[] { "iotHub1/device1/api-version=2010-01-01&DeviceClientType1=customDeviceClient1", "device1", string.Empty, string.Empty, string.Empty };

            yield return new object[] { "iotHub1/device1/api-version=2010-01-01&DeviceClientType1=customDeviceClient1&model-id=testModelId", "device1", string.Empty, string.Empty, "testModelId" };

            yield return new object[] { "iotHub1/device1/module1/api-version=2010-01-01&", "device1", "module1", string.Empty, string.Empty };

            yield return new object[] { "iotHub1/device1/module1/api-version=2010-01-01&model-id=testModelId", "device1", "module1", string.Empty, "testModelId" };

            yield return new object[] { "iotHub1/device1/?api-version=2010-01-01", "device1", string.Empty, string.Empty, string.Empty };

            yield return new object[] { "iotHub1/device1/?api-version=2010-01-01&model-id=testModelId", "device1", string.Empty, string.Empty, "testModelId" };

            yield return new object[] { "iotHub1/device1/module1/?api-version=2010-01-01&Foo=customDeviceClient1", "device1", "module1", string.Empty, string.Empty };
        }

        public static IEnumerable<object[]> GetBadUsernameInputs()
        {
            yield return new[] { "missingEverythingAfterHostname" };
            yield return new[] { "hostname/missingEverthingAfterDeviceId" };
            yield return new[] { "hostname/deviceId/missingApiVersionProperty" };
            yield return new[] { "hostname/deviceId/moduleId/missingApiVersionProperty" };
            yield return new[] { "hostname/deviceId/moduleId/stillMissingApiVersionProperty&DeviceClientType=whatever" };
            yield return new[] { "hostname/deviceId/moduleId/DeviceClientType=whatever&stillMissingApiVersionProperty" };
            yield return new[] { "hostname/deviceId/moduleId/DeviceClientType=stillMissingApiVersionProperty" };
            yield return new[] { "hostname/deviceId/moduleId/api-version=whatever/tooManySegments" };
        }

        [Theory]
        [MemberData(nameof(GetUsernames))]
        [Unit]
        public void ParseUsernameTest(string username, string expectedDeviceId, string expectedModuleId, string expectedDeviceClientType, string expectedModelId)
        {
            var usernameParser = new MqttUsernameParser();

            ClientInfo clientInfo = usernameParser.Parse(username);
            Assert.Equal(expectedDeviceId, clientInfo.DeviceId);
            Assert.Equal(expectedModuleId, clientInfo.ModuleId);
            Assert.Equal(expectedDeviceClientType, clientInfo.DeviceClientType);
            Assert.Equal(!string.IsNullOrWhiteSpace(expectedModelId), clientInfo.ModelId.HasValue);
            clientInfo.ModelId.ForEach(mId => Assert.Equal(expectedModelId, mId));
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
        [InlineData("iotHub1/device1/module1?api-version=2010-01-01?DeviceClientType=customDeviceClient1")]
        [InlineData("iotHub1?api-version=2010-01-01&DeviceClientType=customDeviceClient1")]
        [InlineData("iotHub1/device1/module1/?version=2010-01-01&DeviceClientType=customDeviceClient1")]
        [InlineData("iotHub1//?api-version=2010-01-01&DeviceClientType=customDeviceClient1")]
        [Unit]
        public void ParseUserNameErrorTest(string username)
        {
            var usernameParser = new MqttUsernameParser();

            Assert.Throws<EdgeHubConnectionException>(() => usernameParser.Parse(username));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetBadUsernameInputs))]
        public void NegativeUsernameTest(string username)
        {
            var usernameParser = new MqttUsernameParser();

            Assert.Throws<EdgeHubConnectionException>(() => usernameParser.Parse(username));
        }
    }
}
