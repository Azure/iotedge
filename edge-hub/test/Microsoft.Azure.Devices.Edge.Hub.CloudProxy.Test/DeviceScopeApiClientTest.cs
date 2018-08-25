// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DeviceScopeApiClientTest
    {
        [Fact]
        public void GetServiceUriTest()
        {
            // Arrange
            string iothubHostName = "foo.azure-devices.net";
            string deviceId = "d1";
            string moduleId = "$edgeHub";
            int batchSize = 10;
            var tokenProvider = Mock.Of<ITokenProvider>();
            var deviceScopeApiClient = new DeviceScopeApiClient(iothubHostName, deviceId, moduleId, batchSize, tokenProvider);
            string expectedUri = "https://foo.azure-devices.net/devices/d1/modules/$edgeHub/devicesAndModulesInDeviceScope?deviceCount=10&continuationToken=&api-version=2018-08-30-preview";

            // Act
            Uri uri = deviceScopeApiClient.GetServiceUri(Option.None<string>());

            // Assert
            Assert.NotNull(uri);
            Assert.Equal(expectedUri, uri.ToString());
        }

        [Fact]
        public void GetServiceUriWithContinuationTokenTest()
        {
            // Arrange
            string iothubHostName = "foo.azure-devices.net";
            string deviceId = "d1";
            string moduleId = "$edgeHub";
            int batchSize = 10;
            var tokenProvider = Mock.Of<ITokenProvider>();
            var deviceScopeApiClient = new DeviceScopeApiClient(iothubHostName, deviceId, moduleId, batchSize, tokenProvider);
            string continuationToken = "/devices/d301/modules/%24edgeHub/devicesAndModulesInDeviceScope?deviceCount=10&continuationToken=cccccDDDDDRRRRRsssswJmxhc3Q9bGQyXzE1&api-version=2018-08-30-preview";
            string expectedToken = "https://foo.azure-devices.net/devices/d301/modules/%24edgeHub/devicesAndModulesInDeviceScope?deviceCount=10&continuationToken=cccccDDDDDRRRRRsssswJmxhc3Q9bGQyXzE1&api-version=2018-08-30-preview";

            // Act
            Uri uri = deviceScopeApiClient.GetServiceUri(Option.Some(continuationToken));

            // Assert
            Assert.NotNull(uri);
            Assert.Equal(expectedToken, uri.ToString());
        }

        [Fact]
        public void GetServiceUriForTargetDeviceTest()
        {
            // Arrange
            string iothubHostName = "foo.azure-devices.net";
            string deviceId = "d1";
            string moduleId = "$edgeHub";
            int batchSize = 10;
            var tokenProvider = Mock.Of<ITokenProvider>();
            var deviceScopeApiClient = new DeviceScopeApiClient(iothubHostName, deviceId, moduleId, batchSize, tokenProvider);
            string targetDeviceId = "dev1";
            string targetModuleId = null;
            string expectedToken = "https://foo.azure-devices.net/devices/d1/modules/$edgeHub/deviceAndModuleInDeviceScope?targetDeviceId=dev1&targetModuleId=&api-version=2018-08-30-preview";

            // Act
            Uri uri = deviceScopeApiClient.GetServiceUri(targetDeviceId, targetModuleId);

            // Assert
            Assert.NotNull(uri);
            Assert.Equal(expectedToken, uri.ToString());
        }

        [Fact]
        public void GetServiceUriForTargetModuleTest()
        {
            // Arrange
            string iothubHostName = "foo.azure-devices.net";
            string deviceId = "d1";
            string moduleId = "$edgeHub";
            int batchSize = 10;
            var tokenProvider = Mock.Of<ITokenProvider>();
            var deviceScopeApiClient = new DeviceScopeApiClient(iothubHostName, deviceId, moduleId, batchSize, tokenProvider);
            string targetDeviceId = "dev1";
            string targetModuleId = "mod1";
            string expectedToken = "https://foo.azure-devices.net/devices/d1/modules/$edgeHub/deviceAndModuleInDeviceScope?targetDeviceId=dev1&targetModuleId=mod1&api-version=2018-08-30-preview";

            // Act
            Uri uri = deviceScopeApiClient.GetServiceUri(targetDeviceId, targetModuleId);

            // Assert
            Assert.NotNull(uri);
            Assert.Equal(expectedToken, uri.ToString());
        }
    }
}
