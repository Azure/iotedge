// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class IdentityFactoryTest
    {
        [Fact]
        public void GetWithSasTokenTest_Device()
        {
            string deviceId = "device1";
            string iotHubHostName = "edgehubtest1.azure-devices.net";
            string key = GetRandomString(44);
            string deviceConnectionstring = $"HostName={iotHubHostName};DeviceId={deviceId};SharedAccessKey={key}";

            IIdentityFactory identityFactory = new IdentityFactory(iotHubHostName);
            Try<IIdentity> identityTry = identityFactory.GetWithSasToken(deviceConnectionstring);
            Assert.True(identityTry.Success);
            Assert.NotNull(identityTry.Value);
            IIdentity identity = identityTry.Value;
            Assert.True(identity is IDeviceIdentity);
            Assert.Equal(deviceConnectionstring, identity.ConnectionString);
            Assert.Equal(deviceId, identity.Id);
        }

        [Fact]
        public void GetWithSasTokenTest_Module()
        {
            string deviceId = "device1";
            string moduleId = "module1";
            string iotHubHostName = "edgehubtest1.azure-devices.net";
            string key = GetRandomString(44);
            string deviceConnectionstring = $"HostName={iotHubHostName};DeviceId={deviceId};ModuleId={moduleId};SharedAccessKey={key}";

            IIdentityFactory identityFactory = new IdentityFactory(iotHubHostName);
            Try<IIdentity> identityTry = identityFactory.GetWithSasToken(deviceConnectionstring);
            Assert.True(identityTry.Success);
            Assert.NotNull(identityTry.Value);
            IModuleIdentity identity = identityTry.Value as IModuleIdentity;
            Assert.NotNull(identity);
            Assert.Equal(deviceConnectionstring, identity.ConnectionString);
            Assert.Equal(deviceId, identity.DeviceId);
            Assert.Equal(moduleId, identity.ModuleId);
        }

        static string GetRandomString(int length)
        {
            var rand = new Random();
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[rand.Next(s.Length)]).ToArray());
        }
    }
}
