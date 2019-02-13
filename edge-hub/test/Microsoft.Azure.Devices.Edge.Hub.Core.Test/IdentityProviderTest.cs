// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class IdentityProviderTest
    {
        [Theory]
        [MemberData(nameof(GetCreateTestParameters1))]
        public void CreateIdentityProviderTest(string iothubHostName, string id, IIdentity expectedIdentity)
        {
            // Arrange
            var identityProvider = new IdentityProvider(iothubHostName);

            // Act
            IIdentity identity = identityProvider.Create(id);

            // Assert
            Assert.Equal(expectedIdentity, identity);
            Assert.Equal(expectedIdentity.GetType(), identity.GetType());
        }

        [Theory]
        [MemberData(nameof(GetCreateTestParameters2))]
        public void CreateIdentityProviderTest2(string iothubHostName, string deviceId, string moduleId, IIdentity expectedIdentity)
        {
            // Arrange
            var identityProvider = new IdentityProvider(iothubHostName);

            // Act
            IIdentity identity = identityProvider.Create(deviceId, moduleId);

            // Assert
            Assert.Equal(expectedIdentity, identity);
            Assert.Equal(expectedIdentity.GetType(), identity.GetType());
        }

        public static IEnumerable<object[]> GetCreateTestParameters1()
        {
            yield return new object[] { "foo.azure-device.net", "d1", new DeviceIdentity("foo.azure-device.net", "d1") };
            yield return new object[] { "foo.azure-device.net", "d1/m1", new ModuleIdentity("foo.azure-device.net", "d1", "m1") };
        }

        public static IEnumerable<object[]> GetCreateTestParameters2()
        {
            yield return new object[] { "foo.azure-device.net", "d1", string.Empty, new DeviceIdentity("foo.azure-device.net", "d1") };
            yield return new object[] { "foo.azure-device.net", "d1", "m1", new ModuleIdentity("foo.azure-device.net", "d1", "m1") };
        }
    }
}
