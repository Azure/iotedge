// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class IdentityFactoryTest
    {
        [Fact]
        public void GetWithConnectionStringTest_Device()
        {
            string deviceId = "device1";
            string iotHubHostName = "edgehubtest1.azure-devices.net";
            string key = GetRandomString(44);
            string deviceConnectionstring = $"HostName={iotHubHostName};DeviceId={deviceId};SharedAccessKey={key}";

            IIdentityFactory identityFactory = new IdentityFactory(iotHubHostName);
            Try<IIdentity> identityTry = identityFactory.GetWithConnectionString(deviceConnectionstring);
            Assert.True(identityTry.Success);
            Assert.NotNull(identityTry.Value);
            IIdentity identity = identityTry.Value;
            Assert.True(identity is IDeviceIdentity);
            Assert.Equal(deviceConnectionstring, identity.ConnectionString);
            Assert.Equal(deviceId, identity.Id);
        }

        [Fact]
        public void GetWithConnectionStringTest_Module()
        {
            string deviceId = "device1";
            string moduleId = "module1";
            string iotHubHostName = "edgehubtest1.azure-devices.net";
            string key = GetRandomString(44);
            string deviceConnectionstring = $"HostName={iotHubHostName};DeviceId={deviceId};ModuleId={moduleId};SharedAccessKey={key}";

            IIdentityFactory identityFactory = new IdentityFactory(iotHubHostName);
            Try<IIdentity> identityTry = identityFactory.GetWithConnectionString(deviceConnectionstring);
            Assert.True(identityTry.Success);
            Assert.NotNull(identityTry.Value);
            var identity = identityTry.Value as IModuleIdentity;
            Assert.NotNull(identity);
            Assert.Equal(deviceConnectionstring, identity.ConnectionString);
            Assert.Equal(deviceId, identity.DeviceId);
            Assert.Equal(moduleId, identity.ModuleId);
        }

        static string GetRandomString(int length)
        {
            var rand = new Random();
            const string Chars = "abcdefghijklmnopqrstuvwxyz";
            return new string(Enumerable.Repeat(Chars, length)
              .Select(s => s[rand.Next(s.Length)]).ToArray());
        }

        [Fact]
        public void GetIdentityTest()
        {            
            string iothubHostName = "iothub1.azure.net";
            string callerProductInfo = "foobar";
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/device1/modules/moduleId");

            var identityFactory = new IdentityFactory(iothubHostName, callerProductInfo);

            string username1 = "edgeHub1/device1/api-version=2010-01-01&DeviceClientType=customDeviceClient1";
            string connectionString1 = $"HostName={iothubHostName};DeviceId=device1;SharedAccessSignature={sasToken};X509Cert=False";
            Try<IIdentity> identityTry1 = identityFactory.GetWithSasToken(username1, sasToken);
            Assert.True(identityTry1.Success);
            Assert.IsType<DeviceIdentity>(identityTry1.Value);
            Assert.Equal(connectionString1, identityTry1.Value.ConnectionString);
            Assert.Equal("device1", identityTry1.Value.Id);
            Assert.Equal($"{callerProductInfo} customDeviceClient1", identityTry1.Value.ProductInfo);

            string username2 = "edgeHub1/device1/module1/api-version=2010-01-01&DeviceClientType=customDeviceClient2";
            string connectionString2 = $"HostName={iothubHostName};DeviceId=device1;ModuleId=module1;SharedAccessSignature={sasToken}";
            Try<IIdentity> identityTry2 = identityFactory.GetWithSasToken(username2, sasToken);
            Assert.True(identityTry2.Success);
            Assert.IsType<ModuleIdentity>(identityTry2.Value);
            Assert.Equal(connectionString2, identityTry2.Value.ConnectionString);
            Assert.Equal("device1/module1", identityTry2.Value.Id);
            Assert.Equal($"{callerProductInfo} customDeviceClient2", identityTry2.Value.ProductInfo);
        }

        [Fact]
        public void ParseUserNameTest()
        {
            string username1 = "iotHub1/device1/api-version=2010-01-01&DeviceClientType=customDeviceClient1";
            (string iothubHostName1, string deviceId1, string moduleId1, string deviceClientType1, bool isModuleIdentity1) = IdentityFactory.ParseUserName(username1);
            Assert.Equal("iotHub1", iothubHostName1);
            Assert.Equal("device1", deviceId1);
            Assert.Equal(string.Empty, moduleId1);
            Assert.Equal("customDeviceClient1", deviceClientType1);
            Assert.False(isModuleIdentity1);

            string username2 = "iotHub1/device1/module1/api-version=2010-01-01&DeviceClientType=customDeviceClient2";
            (string iothubHostName2, string deviceId2, string moduleId2, string deviceClientType2, bool isModuleIdentity2) = IdentityFactory.ParseUserName(username2);
            Assert.Equal("iotHub1", iothubHostName2);
            Assert.Equal("device1", deviceId2);
            Assert.Equal("module1", moduleId2);
            Assert.Equal("customDeviceClient2", deviceClientType2);
            Assert.True(isModuleIdentity2);

            string username3 = "iotHub1/device1/module1/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003";
            (string iothubHostName3, string deviceId3, string moduleId3, string deviceClientType3, bool isModuleIdentity3) = IdentityFactory.ParseUserName(username3);
            Assert.Equal("iotHub1", iothubHostName3);
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
        [InlineData("iotHub1/device1/module1/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003/foobar")]
        [InlineData("iotHub1/device1/module1/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client")]
        [InlineData("iotHub1/device1/module1")]
        public void ParseUserNameErrorTest(string username)
        {
            Assert.Throws<EdgeHubConnectionException>(() => IdentityFactory.ParseUserName(username));
        }
    }
}
