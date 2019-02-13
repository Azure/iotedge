// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class ServiceProxyTest
    {
        [Fact]
        public async Task IteratorTest()
        {
            // Arrange
            IEnumerable<Device> devices1 = new[] { GetDevice("d1"), GetDevice("d2"), GetDevice("d3") };
            IEnumerable<Module> modules1 = null;
            string continuationToken1 = "/devices/d301/modules/%24edgeHub/devicesAndModulesInDeviceScope?deviceCount=10&continuationToken=cccccDDDDDRRRRRssssw&api-version=2018-08-30-preview";
            var scopeResult1 = new ScopeResult(devices1, modules1, continuationToken1);

            IEnumerable<Device> devices2 = new[] { GetDevice("d4"), GetDevice("d5") };
            IEnumerable<Module> modules2 = new[] { GetModule("d10", "m1") };
            string continuationToken2 = "/devices/d301/modules/%24edgeHub/devicesAndModulesInDeviceScope?deviceCount=10&continuationToken=cccccbbbbRRRRRssssw&api-version=2018-08-30-preview";
            var scopeResult2 = new ScopeResult(devices2, modules2, continuationToken2);

            IEnumerable<Device> devices3 = null;
            IEnumerable<Module> modules3 = new[] { GetModule("d11", "m1"), GetModule("d11", "m2"), GetModule("d12", "m2") };
            string continuationToken3 = null;
            var scopeResult3 = new ScopeResult(devices3, modules3, continuationToken3);

            var deviceScopeApiResult = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiResult.Setup(d => d.GetIdentitiesInScope())
                .ReturnsAsync(scopeResult1);
            deviceScopeApiResult.SetupSequence(d => d.GetNext(It.IsAny<string>()))
                .ReturnsAsync(scopeResult2)
                .ReturnsAsync(scopeResult3);

            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiResult.Object);

            // Act / Assert
            IServiceIdentitiesIterator iterator = serviceProxy.GetServiceIdentitiesIterator();
            Assert.NotNull(iterator);
            Assert.True(iterator.HasNext);

            IEnumerable<ServiceIdentity> serviceIdentities = await iterator.GetNext();
            Assert.NotNull(serviceIdentities);
            Assert.True(Compare(serviceIdentities, scopeResult1));
            Assert.True(iterator.HasNext);

            serviceIdentities = await iterator.GetNext();
            Assert.NotNull(serviceIdentities);
            Assert.True(Compare(serviceIdentities, scopeResult2));
            Assert.True(iterator.HasNext);

            serviceIdentities = await iterator.GetNext();
            Assert.NotNull(serviceIdentities);
            Assert.True(Compare(serviceIdentities, scopeResult3));
            Assert.False(iterator.HasNext);

            serviceIdentities = await iterator.GetNext();
            Assert.Empty(serviceIdentities);
        }

        [Fact]
        public async Task GetServiceIdentitiy_DeviceTest()
        {
            // Arrange
            IEnumerable<Device> devices1 = new[] { GetDevice("d1") };
            IEnumerable<Module> modules1 = null;
            string continuationToken1 = null;
            var scopeResult1 = new ScopeResult(devices1, modules1, continuationToken1);
            var deviceScopeApiResult = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiResult.Setup(d => d.GetIdentity("d1", null)).ReturnsAsync(scopeResult1);
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiResult.Object);

            // Act
            Option<ServiceIdentity> serviceIdentity = await serviceProxy.GetServiceIdentity("d1");

            // Assert
            Assert.True(serviceIdentity.HasValue);
            Assert.Equal("d1", serviceIdentity.OrDefault().Id);
        }

        [Fact]
        public async Task GetServiceIdentitiy_Device_InvalidTest_MultipleResults()
        {
            // Arrange
            IEnumerable<Device> devices1 = new[] { GetDevice("d1"), GetDevice("d1") };
            IEnumerable<Module> modules1 = null;
            string continuationToken1 = null;
            var scopeResult1 = new ScopeResult(devices1, modules1, continuationToken1);
            var deviceScopeApiResult = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiResult.Setup(d => d.GetIdentity("d1", null)).ReturnsAsync(scopeResult1);
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiResult.Object);

            // Act
            Option<ServiceIdentity> serviceIdentity = await serviceProxy.GetServiceIdentity("d1");

            // Assert
            Assert.False(serviceIdentity.HasValue);
        }

        [Fact]
        public async Task GetServiceIdentitiy_Device_InvalidTest_EmptyResult()
        {
            // Arrange
            IEnumerable<Device> devices1 = new Device[0];
            IEnumerable<Module> modules1 = null;
            string continuationToken1 = null;
            var scopeResult1 = new ScopeResult(devices1, modules1, continuationToken1);
            var deviceScopeApiResult = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiResult.Setup(d => d.GetIdentity("d1", null)).ReturnsAsync(scopeResult1);
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiResult.Object);

            // Act
            Option<ServiceIdentity> serviceIdentity = await serviceProxy.GetServiceIdentity("d1");

            // Assert
            Assert.False(serviceIdentity.HasValue);
        }

        [Fact]
        public async Task GetServiceIdentitiy_Device_InvalidTest_NullResult()
        {
            // Arrange
            ScopeResult scopeResult1 = null;
            var deviceScopeApiResult = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiResult.Setup(d => d.GetIdentity("d1", null)).ReturnsAsync(scopeResult1);
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiResult.Object);

            // Act
            Option<ServiceIdentity> serviceIdentity = await serviceProxy.GetServiceIdentity("d1");

            // Assert
            Assert.False(serviceIdentity.HasValue);
        }

        [Fact]
        public async Task GetServiceIdentitiy_BadRequest_DeviceTest()
        {
            // Arrange
            var deviceScopeApiClient = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiClient.Setup(d => d.GetIdentity("d1", null)).ThrowsAsync(new DeviceScopeApiException("bad request", HttpStatusCode.BadRequest, string.Empty));
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiClient.Object);

            // Act
            Option<ServiceIdentity> serviceIdentity = await serviceProxy.GetServiceIdentity("d1");

            // Assert
            Assert.False(serviceIdentity.HasValue);
        }

        [Fact]
        public async Task GetServiceIdentitiy_Exception_DeviceTest()
        {
            // Arrange
            var exception = new InvalidOperationException();
            var deviceScopeApiClient = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiClient.Setup(d => d.GetIdentity("d1", null)).ThrowsAsync(exception);
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiClient.Object);

            // Act / Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => serviceProxy.GetServiceIdentity("d1"));
        }

        [Fact]
        public async Task GetServiceIdentitiy_BadRequest_ModuleTest()
        {
            // Arrange
            var deviceScopeApiClient = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiClient.Setup(d => d.GetIdentity("d1", "m1")).ThrowsAsync(new DeviceScopeApiException("bad request", HttpStatusCode.BadRequest, string.Empty));
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiClient.Object);

            // Act
            Option<ServiceIdentity> serviceIdentity = await serviceProxy.GetServiceIdentity("d1", "m1");

            // Assert
            Assert.False(serviceIdentity.HasValue);
        }

        [Fact]
        public async Task GetServiceIdentitiy_Exception_ModuleTest()
        {
            // Arrange
            var exception = new InvalidOperationException();
            var deviceScopeApiClient = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiClient.Setup(d => d.GetIdentity("d1", "m1")).ThrowsAsync(exception);
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiClient.Object);

            // Act / Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => serviceProxy.GetServiceIdentity("d1", "m1"));
        }

        [Fact]
        public async Task GetServiceIdentitiy_ModuleTest()
        {
            // Arrange
            IEnumerable<Device> devices1 = null;
            IEnumerable<Module> modules1 = new[] { GetModule("d1", "m1") };

            string continuationToken1 = null;
            var scopeResult1 = new ScopeResult(devices1, modules1, continuationToken1);
            var deviceScopeApiResult = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiResult.Setup(d => d.GetIdentity("d1", "m1")).ReturnsAsync(scopeResult1);
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiResult.Object);

            // Act
            Option<ServiceIdentity> serviceIdentity = await serviceProxy.GetServiceIdentity("d1", "m1");

            // Assert
            Assert.True(serviceIdentity.HasValue);
            Assert.Equal("d1/m1", serviceIdentity.OrDefault().Id);
        }

        [Fact]
        public async Task GetServiceIdentitiy_Module_InvalidTest_MultipleResults()
        {
            // Arrange
            IEnumerable<Device> devices1 = null;
            IEnumerable<Module> modules1 = new[] { GetModule("d1", "m1"), GetModule("d1", "m1") };

            string continuationToken1 = null;
            var scopeResult1 = new ScopeResult(devices1, modules1, continuationToken1);
            var deviceScopeApiResult = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiResult.Setup(d => d.GetIdentity("d1", "m1")).ReturnsAsync(scopeResult1);
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiResult.Object);

            // Act
            Option<ServiceIdentity> serviceIdentity = await serviceProxy.GetServiceIdentity("d1", "m1");

            // Assert
            Assert.False(serviceIdentity.HasValue);
        }

        [Fact]
        public async Task GetServiceIdentitiy_Module_InvalidTest_EmptyResult()
        {
            // Arrange
            IEnumerable<Device> devices1 = null;
            IEnumerable<Module> modules1 = new Module[0];

            string continuationToken1 = null;
            var scopeResult1 = new ScopeResult(devices1, modules1, continuationToken1);
            var deviceScopeApiResult = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiResult.Setup(d => d.GetIdentity("d1", "m1")).ReturnsAsync(scopeResult1);
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiResult.Object);

            // Act
            Option<ServiceIdentity> serviceIdentity = await serviceProxy.GetServiceIdentity("d1", "m1");

            // Assert
            Assert.False(serviceIdentity.HasValue);
        }

        [Fact]
        public async Task GetServiceIdentitiy_Module_InvalidTest_NullResult()
        {
            // Arrange
            ScopeResult scopeResult1 = null;
            var deviceScopeApiResult = new Mock<IDeviceScopeApiClient>();
            deviceScopeApiResult.Setup(d => d.GetIdentity("d1", "m1")).ReturnsAsync(scopeResult1);
            IServiceProxy serviceProxy = new ServiceProxy(deviceScopeApiResult.Object);

            // Act
            Option<ServiceIdentity> serviceIdentity = await serviceProxy.GetServiceIdentity("d1", "m1");

            // Assert
            Assert.False(serviceIdentity.HasValue);
        }

        static bool Compare(IEnumerable<ServiceIdentity> serviceIdentities, ScopeResult scopeResult)
        {
            List<ServiceIdentity> serviceIdentitiesList = serviceIdentities.ToList();
            if (scopeResult.Devices != null)
            {
                foreach (Device d in scopeResult.Devices)
                {
                    if (!serviceIdentitiesList.Any(s => s.DeviceId == d.Id && !s.IsModule))
                    {
                        return false;
                    }
                }
            }

            if (scopeResult.Modules != null)
            {
                foreach (Module m in scopeResult.Modules)
                {
                    if (!serviceIdentitiesList.Any(s => s.DeviceId == m.DeviceId && s.ModuleId.OrDefault() == m.Id && s.IsModule))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static Device GetDevice(string id)
        {
            string deviceJson = @"{
                      ""deviceId"": """ + id + @""",
                      ""generationId"": ""12345"",
                      ""etag"": ""0000"",
                      ""connectionState"": ""Disconnected"",
                      ""status"": ""enabled"",
                      ""statusReason"": null,
                      ""connectionStateUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""statusUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""lastActivityTime"": ""0001-01-01T00:00:00"",
                      ""cloudToDeviceMessageCount"": 0,
                      ""authentication"": {
                        ""symmetricKey"": {
                          ""primaryKey"": """ + GetKey() + @""",
                          ""secondaryKey"": """ + GetKey() + @"""
                        },
                        ""x509Thumbprint"": {
                          ""primaryThumbprint"": null,
                          ""secondaryThumbprint"": null
                        },
                        ""type"": ""sas""
                      },
                      ""capabilities"": {
                        ""iotEdge"": true
                      },
                      ""deviceScope"": ""ms-azure-iot-edge://d301-12345""
                }";
            var device = JsonConvert.DeserializeObject<Device>(deviceJson);
            return device;
        }

        static Module GetModule(string deviceId, string moduleId)
        {
            string moduleJson = @"{                        
                      ""deviceId"": """ + deviceId + @""",
                      ""moduleId"": """ + moduleId + @""",
                      ""managedBy"": ""iotEdge"",
                      ""generationId"": ""636704968692034950"",
                      ""etag"": ""NzM0NTkyNTc="",
                      ""connectionState"": ""Disconnected"",
                      ""statusReason"": null,
                      ""connectionStateUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""statusUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""lastActivityTime"": ""0001-01-01T00:00:00"",
                      ""cloudToDeviceMessageCount"": 0,
                      ""authentication"": {
                        ""symmetricKey"": {
                          ""primaryKey"": """ + GetKey() + @""",
                          ""secondaryKey"": """ + GetKey() + @"""
                        },
                        ""x509Thumbprint"": {
                          ""primaryThumbprint"": null,
                          ""secondaryThumbprint"": null
                        },
                        ""type"": ""sas""
                      }
                }";
            var module = JsonConvert.DeserializeObject<Module>(moduleJson);
            return module;
        }

        static string GetKey() => Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
    }
}
