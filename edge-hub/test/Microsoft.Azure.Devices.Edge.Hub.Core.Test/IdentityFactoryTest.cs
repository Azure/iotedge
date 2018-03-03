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
        public void GetSASIdentityTest()
        {
            string iothubHostName = "iothub1.azure.net";
            string callerProductInfo = "productInfo";
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/device1/modules/moduleId");

            var identityFactory = new IdentityFactory(iothubHostName, callerProductInfo);

            // device test
            string deviceId = "device1";
            string deviceClientType = "customDeviceClient1";
            string connectionString1 = $"HostName={iothubHostName};DeviceId=device1;SharedAccessSignature={sasToken};X509Cert=False";
            Try<IIdentity> identityTry1 = identityFactory.GetWithSasToken(deviceId, null, deviceClientType, false, sasToken);
            Assert.True(identityTry1.Success);
            Assert.IsType<DeviceIdentity>(identityTry1.Value);
            Assert.Equal(connectionString1, identityTry1.Value.ConnectionString);
            Assert.Equal("device1", identityTry1.Value.Id);
            Assert.Equal($"{callerProductInfo} customDeviceClient1", identityTry1.Value.ProductInfo);
            Assert.Equal(AuthenticationScope.SasToken, identityTry1.Value.Scope);

            // module test
            deviceId = "device1";
            string moduleId = "module1";
            deviceClientType = "customDeviceClient2";
            string connectionString2 = $"HostName={iothubHostName};DeviceId=device1;ModuleId=module1;SharedAccessSignature={sasToken}";
            Try<IIdentity> identityTry2 = identityFactory.GetWithSasToken(deviceId, moduleId, deviceClientType, true, sasToken);
            Assert.True(identityTry2.Success);
            Assert.IsType<ModuleIdentity>(identityTry2.Value);
            Assert.Equal(connectionString2, identityTry2.Value.ConnectionString);
            Assert.Equal("device1/module1", identityTry2.Value.Id);
            Assert.Equal($"{callerProductInfo} customDeviceClient2", identityTry2.Value.ProductInfo);
            Assert.Equal(AuthenticationScope.SasToken, identityTry1.Value.Scope);
        }

        [Fact]
        public void GetX509IdentityTest()
        {
            string iothubHostName = "iothub1.azure.net";
            string callerProductInfo = "productInfo";
            string deviceId = "device1";
            string moduleId = "module1";
            string deviceClientType = "customDeviceClient1";
            var identityFactory = new IdentityFactory(iothubHostName, callerProductInfo);

            // device test
            Try<IIdentity> identityTry1 = identityFactory.GetWithX509Cert(deviceId, null, deviceClientType, false);
            Assert.True(identityTry1.Success);
            Assert.IsType<DeviceIdentity>(identityTry1.Value);
            Assert.Equal(null, identityTry1.Value.ConnectionString);
            Assert.Equal("device1", identityTry1.Value.Id);
            Assert.Equal($"{callerProductInfo} customDeviceClient1", identityTry1.Value.ProductInfo);
            Assert.Equal(AuthenticationScope.x509Cert, identityTry1.Value.Scope);
            Assert.Equal(Option.None<string>(), identityTry1.Value.Token);

            // module test
            Try<IIdentity> identityTry2 = identityFactory.GetWithX509Cert(deviceId, moduleId, deviceClientType, true);
            Assert.True(identityTry2.Success);
            Assert.IsType<ModuleIdentity>(identityTry2.Value);
            Assert.Equal(null, identityTry2.Value.ConnectionString);
            Assert.Equal("device1/module1", identityTry2.Value.Id);
            Assert.Equal($"{callerProductInfo} customDeviceClient1", identityTry2.Value.ProductInfo);
            Assert.Equal(AuthenticationScope.x509Cert, identityTry2.Value.Scope);
            Assert.Equal(Option.None<string>(), identityTry2.Value.Token);
        }
    }
}
