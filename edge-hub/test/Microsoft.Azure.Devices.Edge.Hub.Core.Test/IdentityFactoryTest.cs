// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Linq;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Xunit;

    [Unit]
    public class IdentityFactoryTest
    {
        [Fact]
        public void GetSasIdentityTest()
        {
            string iothubHostName = "iothub1.azure.net";
            string callerProductInfo = "productInfo";
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/device1/modules/moduleId");

            var identityFactory = new ClientCredentialsFactory(iothubHostName, callerProductInfo);

            // device test
            string deviceId = "device1";
            string deviceClientType = "customDeviceClient1";
            IClientCredentials identityTry1 = identityFactory.GetWithSasToken(deviceId, null, deviceClientType, sasToken, false);
            Assert.IsType<DeviceIdentity>(identityTry1.Identity);
            Assert.IsType<TokenCredentials>(identityTry1);
            Assert.Equal(sasToken, (identityTry1 as ITokenCredentials)?.Token);
            Assert.Equal("device1", identityTry1.Identity.Id);
            Assert.Equal($"{callerProductInfo} customDeviceClient1", identityTry1.ProductInfo);
            Assert.Equal(AuthenticationType.Token, identityTry1.AuthenticationType);

            // module test
            deviceId = "device1";
            string moduleId = "module1";
            deviceClientType = "customDeviceClient2";
            IClientCredentials identityTry2 = identityFactory.GetWithSasToken(deviceId, moduleId, deviceClientType, sasToken, false);
            Assert.IsType<ModuleIdentity>(identityTry2.Identity);
            Assert.IsType<TokenCredentials>(identityTry2);
            Assert.Equal(sasToken, (identityTry2 as ITokenCredentials)?.Token);
            Assert.Equal("device1/module1", identityTry2.Identity.Id);
            Assert.Equal($"{callerProductInfo} customDeviceClient2", identityTry2.ProductInfo);
            Assert.Equal(AuthenticationType.Token, identityTry2.AuthenticationType);
        }

        [Fact]
        public void GetWithConnectionStringTest_Device()
        {
            string deviceId = "device1";
            string iotHubHostName = "edgehubtest1.azure-devices.net";
            string key = GetRandomString(44);
            string deviceConnectionstring = $"HostName={iotHubHostName};DeviceId={deviceId};SharedAccessKey={key}";

            IClientCredentialsFactory identityFactory = new ClientCredentialsFactory(iotHubHostName);
            IClientCredentials identityTry = identityFactory.GetWithConnectionString(deviceConnectionstring);
            Assert.NotNull(identityTry);
            IIdentity identity = identityTry.Identity;
            Assert.IsType<DeviceIdentity>(identity);
            Assert.IsType<SharedKeyCredentials>(identityTry);
            Assert.Equal(deviceConnectionstring, (identityTry as ISharedKeyCredentials)?.ConnectionString);
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

            IClientCredentialsFactory identityFactory = new ClientCredentialsFactory(iotHubHostName);
            IClientCredentials identityTry = identityFactory.GetWithConnectionString(deviceConnectionstring);
            Assert.NotNull(identityTry);
            var identity = identityTry.Identity as IModuleIdentity;
            Assert.NotNull(identity);
            Assert.IsType<SharedKeyCredentials>(identityTry);
            Assert.Equal(deviceConnectionstring, (identityTry as ISharedKeyCredentials)?.ConnectionString);
            Assert.Equal(deviceId, identity.DeviceId);
            Assert.Equal(moduleId, identity.ModuleId);
        }

        [Fact]
        public void GetX509IdentityTest()
        {
            string iothubHostName = "iothub1.azure.net";
            string callerProductInfo = "productInfo";
            string deviceId = "device1";
            string moduleId = "module1";
            string deviceClientType = "customDeviceClient1";
            var identityFactory = new ClientCredentialsFactory(iothubHostName, callerProductInfo);

            // device test
            IClientCredentials identityTry1 = identityFactory.GetWithX509Cert(deviceId, null, deviceClientType);
            Assert.IsType<DeviceIdentity>(identityTry1.Identity);
            Assert.Equal("device1", identityTry1.Identity.Id);
            Assert.Equal($"{callerProductInfo} customDeviceClient1", identityTry1.ProductInfo);
            Assert.Equal(AuthenticationType.X509Cert, identityTry1.AuthenticationType);

            // module test
            IClientCredentials identityTry2 = identityFactory.GetWithX509Cert(deviceId, moduleId, deviceClientType);
            Assert.IsType<ModuleIdentity>(identityTry2.Identity);
            Assert.Equal("device1/module1", identityTry2.Identity.Id);
            Assert.Equal($"{callerProductInfo} customDeviceClient1", identityTry2.ProductInfo);
            Assert.Equal(AuthenticationType.X509Cert, identityTry1.AuthenticationType);
        }

        static string GetRandomString(int length)
        {
            var rand = new Random();
            const string Chars = "abcdefghijklmnopqrstuvwxyz";
            return new string(
                Enumerable.Repeat(Chars, length)
                    .Select(s => s[rand.Next(s.Length)]).ToArray());
        }
    }
}
