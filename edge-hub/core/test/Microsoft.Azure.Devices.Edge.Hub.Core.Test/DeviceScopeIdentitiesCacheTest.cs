// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Query.Builtins;
    using Moq;
    using Xunit;

    [Unit]
    public class DeviceScopeIdentitiesCacheTest
    {
        [Fact]
        public async Task InitializeFromStoreTest()
        {
            // Arrange
            var iterator = new Mock<IServiceIdentitiesIterator>();
            iterator.Setup(i => i.HasNext).Returns(true);
            iterator.Setup(i => i.GetNext()).ThrowsAsync(new InvalidOperationException("Some error"));
            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentitiesIterator()).Returns(iterator.Object);

            var store = GetEntityStore("cache");
            var serviceAuthentication = new ServiceAuthentication(ServiceAuthenticationType.None);
            var si1 = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            var si2 = new ServiceIdentity("d2", "m1", "e1", Enumerable.Empty<string>(), "2345", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            var storedSi1 = new DeviceScopeIdentitiesCache.StoredServiceIdentity(si1);
            await store.Put(si1.Id, storedSi1.ToJson());
            var storedSi2 = new DeviceScopeIdentitiesCache.StoredServiceIdentity(si2);
            await store.Put(si2.Id, storedSi2.ToJson());

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));
            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m1");

            // Assert
            Assert.True(si1.Equals(receivedServiceIdentity1.OrDefault()));
            Assert.True(si2.Equals(receivedServiceIdentity2.OrDefault()));
        }

        [Fact]
        public async Task RefreshCacheTest()
        {
            // Arrange
            var store = GetEntityStore("cache");
            var serviceAuthentication = new ServiceAuthentication(ServiceAuthenticationType.None);
            Func<ServiceIdentity> si1 = () => new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            Func<ServiceIdentity> si2 = () => new ServiceIdentity("d2", "m1", "e1", Enumerable.Empty<string>(), "2345", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            Func<ServiceIdentity> si3 = () => new ServiceIdentity("d3", "5678", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            Func<ServiceIdentity> si4 = () => new ServiceIdentity("d2", "m4", "e1", Enumerable.Empty<string>(), "9898", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);

            var iterator1 = new Mock<IServiceIdentitiesIterator>();
            iterator1.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(true)
                .Returns(false);
            iterator1.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1(),
                        si2()
                    })
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si3(),
                        si4()
                    });

            var iterator2 = new Mock<IServiceIdentitiesIterator>();
            iterator2.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator2.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1(),
                        si2(),
                        si3()
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.SetupSequence(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator1.Object)
                .Returns(iterator2.Object);
            var updatedIdentities = new List<ServiceIdentity>();
            var removedIdentities = new List<string>();
            IList<string> entireCache = new List<string>();

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromMinutes(60), TimeSpan.FromSeconds(3));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);
            deviceScopeIdentitiesCache.ServiceIdentitiesUpdated += (sender, serviceIdentities) => entireCache = serviceIdentities;

            // Wait for refresh to complete
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));
            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m1");
            Option<ServiceIdentity> receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");
            Option<ServiceIdentity> receivedServiceIdentity4 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m4");

            // Assert
            Assert.Equal(si1(), receivedServiceIdentity1.OrDefault());
            Assert.Equal(si2(), receivedServiceIdentity2.OrDefault());
            Assert.Equal(si3(), receivedServiceIdentity3.OrDefault());
            Assert.Equal(si4(), receivedServiceIdentity4.OrDefault());

            // Wait for another refresh cycle to complete, delay more than refreshDelay to make sure refresh is initiated
            await Task.Delay(TimeSpan.FromSeconds(4));
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));

            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m1");
            receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");
            receivedServiceIdentity4 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m4");

            // Assert
            Assert.Equal(si1(), receivedServiceIdentity1.OrDefault());
            Assert.Equal(si2(), receivedServiceIdentity2.OrDefault());
            Assert.Equal(si3(), receivedServiceIdentity3.OrDefault());
            Assert.False(receivedServiceIdentity4.HasValue);
            Assert.Equal(3, entireCache.Count);

            Assert.Contains(si1().Id, entireCache);
            Assert.Contains(si2().Id, entireCache);
            Assert.Contains(si3().Id, entireCache);
        }

        [Fact(Skip = "Consistently flakey test - bug logged")]
        public async Task RefreshCacheWithRefreshRequestTest()
        {
            // Arrange
            var store = GetEntityStore("cache");
            var serviceAuthentication = new ServiceAuthentication(ServiceAuthenticationType.None);
            Func<ServiceIdentity> si1 = () => new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            Func<ServiceIdentity> si2 = () => new ServiceIdentity("d2", "m1", "e1", Enumerable.Empty<string>(), "2345", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            Func<ServiceIdentity> si3 = () => new ServiceIdentity("d3", "5678", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            Func<ServiceIdentity> si4 = () => new ServiceIdentity("d2", "m4", "e1", Enumerable.Empty<string>(), "9898", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);

            var iterator1 = new Mock<IServiceIdentitiesIterator>();
            iterator1.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(true)
                .Returns(false);
            iterator1.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1(),
                        si2()
                    })
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si3(),
                        si4()
                    });

            var iterator2 = new Mock<IServiceIdentitiesIterator>();
            iterator2.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator2.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1(),
                        si2(),
                        si3()
                    });

            var iterator3 = new Mock<IServiceIdentitiesIterator>();
            iterator3.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator3.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1(),
                        si2()
                    });

            var iterator4 = new Mock<IServiceIdentitiesIterator>();
            iterator4.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator4.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si3(),
                        si4()
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.SetupSequence(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator1.Object)
                .Returns(iterator2.Object)
                .Returns(iterator3.Object)
                .Returns(iterator4.Object);
            var updatedIdentities = new List<ServiceIdentity>();
            var removedIdentities = new List<string>();
            IList<string> entireCache = new List<string>();

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(0));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);
            deviceScopeIdentitiesCache.ServiceIdentitiesUpdated += (sender, serviceIdentities) => entireCache = serviceIdentities;

            // Wait for refresh to complete
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));

            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m1");
            Option<ServiceIdentity> receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");
            Option<ServiceIdentity> receivedServiceIdentity4 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m4");

            // Assert
            Assert.True(si1().Equals(receivedServiceIdentity1.OrDefault()));
            Assert.True(si2().Equals(receivedServiceIdentity2.OrDefault()));
            Assert.True(si3().Equals(receivedServiceIdentity3.OrDefault()));
            Assert.True(si4().Equals(receivedServiceIdentity4.OrDefault()));

            Assert.Empty(updatedIdentities);
            Assert.Empty(removedIdentities);

            Assert.Equal(4, entireCache.Count);
            Assert.Contains(si1().Id, entireCache);
            Assert.Contains(si2().Id, entireCache);
            Assert.Contains(si3().Id, entireCache);
            Assert.Contains(si4().Id, entireCache);

            // Act - Signal refresh cache multiple times. It should get picked up twice.
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            deviceScopeIdentitiesCache.InitiateCacheRefresh();

            // Wait for the 2 refresh cycles to complete, this time because of the refresh request
            await Task.Delay(TimeSpan.FromSeconds(5));
            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m1");
            receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");
            receivedServiceIdentity4 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m4");

            // Assert
            Assert.True(si1().Equals(receivedServiceIdentity1.OrDefault()));
            Assert.True(si2().Equals(receivedServiceIdentity2.OrDefault()));
            Assert.False(receivedServiceIdentity3.HasValue);
            Assert.False(receivedServiceIdentity4.HasValue);

            Assert.Empty(updatedIdentities);
            Assert.Equal(2, removedIdentities.Count);
            Assert.Contains("d2/m4", removedIdentities);
            Assert.Contains("d3", removedIdentities);
            Assert.Contains("d1", removedIdentities);
            Assert.Contains("d2/m1", removedIdentities);

            // Wait for another refresh cycle to complete, this time because timeout
            await Task.Delay(TimeSpan.FromSeconds(8));
            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m1");
            receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");
            receivedServiceIdentity4 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m4");

            // Assert
            Assert.True(si3().Equals(receivedServiceIdentity3.OrDefault()));
            Assert.True(si4().Equals(receivedServiceIdentity4.OrDefault()));
            Assert.False(receivedServiceIdentity1.HasValue);
            Assert.False(receivedServiceIdentity2.HasValue);

            Assert.Equal(2, entireCache.Count);
            Assert.Contains(si3().Id, entireCache);
            Assert.Contains(si4().Id, entireCache);

            Assert.Empty(updatedIdentities);
            Assert.Equal(4, removedIdentities.Count);
            Assert.Contains("d2/m1", removedIdentities);
            Assert.Contains("d1", removedIdentities);
        }

        [Fact]
        public async Task RefreshServiceIdentityTest_Device()
        {
            // Arrange
            var store = GetEntityStore("cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            var si1_initial = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthenticationNone, ServiceIdentityStatus.Enabled);
            var si1_updated = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);
            var si2 = new ServiceIdentity("d2", "5678", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);

            var iterator1 = new Mock<IServiceIdentitiesIterator>();
            iterator1.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator1.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_initial,
                        si2
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator1.Object);
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d1"), It.IsAny<string>())).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d2"), It.IsAny<string>())).ReturnsAsync(Option.None<ServiceIdentity>());

            var updatedIdentities = new List<ServiceIdentity>();
            var removedIdentities = new List<string>();
            IList<string> entireCache = new List<string>();

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);
            deviceScopeIdentitiesCache.ServiceIdentitiesUpdated += (sender, serviceIdentities) => entireCache = serviceIdentities;

            // Wait for refresh to complete
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));
            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2");

            // Assert
            Assert.True(si1_initial.Equals(receivedServiceIdentity1.OrDefault()));
            Assert.True(si2.Equals(receivedServiceIdentity2.OrDefault()));

            // Update the identities
            await deviceScopeIdentitiesCache.RefreshServiceIdentity("d1");
            await deviceScopeIdentitiesCache.RefreshServiceIdentity("d2");

            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2");

            // Assert
            Assert.True(si1_updated.Equals(receivedServiceIdentity1.OrDefault()));
            Assert.False(receivedServiceIdentity2.HasValue);
            Assert.Single(removedIdentities);
            Assert.Equal("d2", removedIdentities[0]);
            Assert.Single(updatedIdentities);
            Assert.Equal("d1", updatedIdentities[0].Id);
            Assert.Equal(1, entireCache.Count);
            Assert.Contains(si1_updated.Id, entireCache);
        }

        [Fact]
        public async Task RefreshServiceIdentityTest_List()
        {
            // Arrange
            var store = GetEntityStore("cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            var si1_initial = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthenticationNone, ServiceIdentityStatus.Enabled);
            var si1_updated = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);
            var si2 = new ServiceIdentity("d2", "5678", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);

            var iterator1 = new Mock<IServiceIdentitiesIterator>();
            iterator1.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator1.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_initial,
                        si2
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator1.Object);
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d1"), It.IsAny<string>())).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d2"), It.IsAny<string>())).ReturnsAsync(Option.None<ServiceIdentity>());
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d3"), It.IsAny<string>())).ReturnsAsync(Option.None<ServiceIdentity>());

            var updatedIdentities = new List<ServiceIdentity>();
            var removedIdentities = new List<string>();
            IList<string> entireCache = new List<string>();

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);
            deviceScopeIdentitiesCache.ServiceIdentitiesUpdated += (sender, serviceIdentities) => entireCache = serviceIdentities;

            // Wait for refresh to complete
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));
            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2");
            Option<ServiceIdentity> receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");

            // Assert
            Assert.True(si1_initial.Equals(receivedServiceIdentity1.OrDefault()));
            Assert.True(si2.Equals(receivedServiceIdentity2.OrDefault()));
            Assert.False(receivedServiceIdentity3.HasValue);

            // Update the identities
            await deviceScopeIdentitiesCache.RefreshServiceIdentities(new[] { "d1", "d2", "d3" });

            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2");
            receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");

            // Assert
            Assert.True(si1_updated.Equals(receivedServiceIdentity1.OrDefault()));
            Assert.False(receivedServiceIdentity2.HasValue);
            Assert.False(receivedServiceIdentity3.HasValue);
            Assert.Single(removedIdentities);
            Assert.Equal("d2", removedIdentities[0]);
            Assert.Single(updatedIdentities);
            Assert.Equal("d1", updatedIdentities[0].Id);
            Assert.Equal(1, entireCache.Count);
            Assert.Contains(si1_updated.Id, entireCache);
        }

        [Fact]
        public async Task RefreshServiceIdentityTest_Module()
        {
            // Arrange
            var store = GetEntityStore("cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            var si1_initial = new ServiceIdentity("d1", "m1", "e1", Enumerable.Empty<string>(), "1234", Enumerable.Empty<string>(), serviceAuthenticationNone, ServiceIdentityStatus.Enabled);
            var si1_updated = new ServiceIdentity("d1", "m1", "e1", Enumerable.Empty<string>(), "1234", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);
            var si2 = new ServiceIdentity("d2", "m2", "e1", Enumerable.Empty<string>(), "5678", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);

            var iterator1 = new Mock<IServiceIdentitiesIterator>();
            iterator1.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator1.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_initial,
                        si2
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator1.Object);
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d1"), It.Is<string>(id => id == "m1"), It.IsAny<string>())).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d2"), It.Is<string>(id => id == "m2"), It.IsAny<string>())).ReturnsAsync(Option.None<ServiceIdentity>());

            var updatedIdentities = new List<ServiceIdentity>();
            var removedIdentities = new List<string>();
            IList<string> entireCache = new List<string>();

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);
            deviceScopeIdentitiesCache.ServiceIdentitiesUpdated += (sender, serviceIdentities) => entireCache = serviceIdentities;

            // Wait for refresh to complete
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));
            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1/m1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m2");

            // Assert
            Assert.True(si1_initial.Equals(receivedServiceIdentity1.OrDefault()));
            Assert.True(si2.Equals(receivedServiceIdentity2.OrDefault()));

            // Update the identities
            await deviceScopeIdentitiesCache.RefreshServiceIdentity("d1/m1");
            await deviceScopeIdentitiesCache.RefreshServiceIdentity("d2/m2");

            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1/m1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m2");

            // Assert
            Assert.True(si1_updated.Equals(receivedServiceIdentity1.OrDefault()));
            Assert.False(receivedServiceIdentity2.HasValue);
            Assert.Single(removedIdentities);
            Assert.Equal("d2/m2", removedIdentities[0]);
            Assert.Single(updatedIdentities);
            Assert.Equal("d1/m1", updatedIdentities[0].Id);
            Assert.Equal(1, entireCache.Count);
            Assert.Contains(si1_updated.Id, entireCache);
        }

        [Fact]
        public async Task GetServiceIdentityTest_Device()
        {
            // Arrange
            var store = GetEntityStore("cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            var si1_initial = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthenticationNone, ServiceIdentityStatus.Enabled);
            var si1_updated = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthenticationNone, ServiceIdentityStatus.Disabled);
            var si2 = new ServiceIdentity("d2", "5678", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);
            var si3 = new ServiceIdentity("d3", "0987", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);

            var iterator1 = new Mock<IServiceIdentitiesIterator>();
            iterator1.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator1.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_initial,
                        si2
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator1.Object);
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d1"), It.IsAny<string>())).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d2"), It.IsAny<string>())).ReturnsAsync(Option.None<ServiceIdentity>());
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d3"), It.IsAny<string>())).ReturnsAsync(Option.Some(si3));

            var updatedIdentities = new List<ServiceIdentity>();
            var removedIdentities = new List<string>();

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);

            // Wait for refresh to complete
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));

            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2");
            Option<ServiceIdentity> receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");

            // Assert
            Assert.True(si1_initial.Equals(receivedServiceIdentity1.OrDefault()));
            Assert.True(si2.Equals(receivedServiceIdentity2.OrDefault()));
            Assert.False(receivedServiceIdentity3.HasValue);
        }

        [Fact]
        public async Task GetServiceIdentityTest_Module()
        {
            // Arrange
            var store = GetEntityStore("cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            var si1_initial = new ServiceIdentity("d1", "m1", "e1", Enumerable.Empty<string>(), "1234", Enumerable.Empty<string>(), serviceAuthenticationNone, ServiceIdentityStatus.Enabled);
            var si1_updated = new ServiceIdentity("d1", "m1", "e1", Enumerable.Empty<string>(), "1234", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Disabled);
            var si2 = new ServiceIdentity("d2", "m2", "e1", Enumerable.Empty<string>(), "5678", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);
            var si3 = new ServiceIdentity("d3", "m3", "e1", Enumerable.Empty<string>(), "0987", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);

            var iterator1 = new Mock<IServiceIdentitiesIterator>();
            iterator1.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator1.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_initial,
                        si2
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator1.Object);
            serviceProxy.Setup(s => s.GetServiceIdentity("d1", "m1", It.IsAny<string>())).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity("d2", "m2", It.IsAny<string>())).ReturnsAsync(Option.None<ServiceIdentity>());
            serviceProxy.Setup(s => s.GetServiceIdentity("d3", "m3", It.IsAny<string>())).ReturnsAsync(Option.Some(si3));

            var updatedIdentities = new List<ServiceIdentity>();
            var removedIdentities = new List<string>();

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);

            // Wait for refresh to complete
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));

            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1/m1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m2");
            Option<ServiceIdentity> receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3/m3");

            // Assert
            Assert.True(si1_initial.Equals(receivedServiceIdentity1.OrDefault()));
            Assert.True(si2.Equals(receivedServiceIdentity2.OrDefault()));
            Assert.False(receivedServiceIdentity3.HasValue);
        }

        [Fact]
        public async Task GetServiceIdentityFromServiceTest()
        {
            // Arrange
            var store = GetEntityStore("cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            var si_device = new ServiceIdentity("d2", "1234", Enumerable.Empty<string>(), serviceAuthenticationNone, ServiceIdentityStatus.Enabled);
            var si_module = new ServiceIdentity("d1", "m1", "e1", Enumerable.Empty<string>(), "1234", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Disabled);

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentity("d2", It.IsAny<string>())).ReturnsAsync(Option.Some(si_device));
            serviceProxy.Setup(s => s.GetServiceIdentity("d1", "m1", It.IsAny<string>())).ReturnsAsync(Option.Some(si_module));

            string edgeDeviceId = "deviceId";
            DeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree(edgeDeviceId), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act
            Option<ServiceIdentity> deviceServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentityFromService("d2", edgeDeviceId);
            Option<ServiceIdentity> moduleServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentityFromService("d1/m1", edgeDeviceId);

            // Assert
            Assert.True(deviceServiceIdentity.HasValue);
            Assert.True(si_device.Equals(deviceServiceIdentity.OrDefault()));
            Assert.True(moduleServiceIdentity.HasValue);
            Assert.True(si_module.Equals(moduleServiceIdentity.OrDefault()));
        }

        [Fact]
        public async Task GetAuthChainTest()
        {
            // Arrange
            var store = GetEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string parentEdgeId = "parentEdge";
            string childEdgeId = "childEdge";
            string leafId = "leaf";
            var parentEdge = new ServiceIdentity(parentEdgeId, "1234", edgeCapability, serviceAuth, ServiceIdentityStatus.Enabled);
            var childEdge = new ServiceIdentity(childEdgeId, "1234", edgeCapability, serviceAuth, ServiceIdentityStatus.Enabled);
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);
            string authChain = leafId + ";" + childEdgeId + ";" + parentEdgeId;

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();
            serviceIdentityHierarchy.Setup(s => s.Get(parentEdgeId)).ReturnsAsync(Option.Some(parentEdge));
            serviceIdentityHierarchy.Setup(s => s.Get(childEdgeId)).ReturnsAsync(Option.Some(childEdge));
            serviceIdentityHierarchy.Setup(s => s.Get(leafId)).ReturnsAsync(Option.Some(leaf));
            serviceIdentityHierarchy.Setup(s => s.GetAuthChain(leafId)).ReturnsAsync(Option.Some(authChain));
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns(parentEdgeId);

            var identitiesIterator = new Mock<IServiceIdentitiesIterator>();
            identitiesIterator.Setup(i => i.HasNext).Returns(false);
            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentitiesIterator()).Returns(identitiesIterator.Object);

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act
            Option<string> authChainActual = await deviceScopeIdentitiesCache.GetAuthChain(leafId);

            // Assert
            Assert.True(authChainActual.Contains(authChain));
        }

        [Fact]
        public async Task RefreshAuthChainTest()
        {
            // Arrange
            var store = GetEntityStore("cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);

            string id1 = "d1";
            string id2 = "d2";
            string id3 = "d3";
            var si1_initial = new ServiceIdentity(id1, null, "d1_scope", Enumerable.Empty<string>(), "1234", new List<string>() { Constants.IotEdgeIdentityCapability }, serviceAuthenticationNone, ServiceIdentityStatus.Enabled);
            var si1_updated = new ServiceIdentity(id1, null, "d1_scope", Enumerable.Empty<string>(), "1234", new List<string>() { Constants.IotEdgeIdentityCapability }, serviceAuthenticationNone, ServiceIdentityStatus.Disabled);
            var si2 = new ServiceIdentity(id2, null, "d2_scope", new List<string>() { "d1_scope" }, "1234", new List<string>() { Constants.IotEdgeIdentityCapability }, serviceAuthenticationNone, ServiceIdentityStatus.Enabled);
            var si3 = new ServiceIdentity(id3, null, null, new List<string>() { "d2_scope" }, "1234", Enumerable.Empty<string>(), serviceAuthenticationNone, ServiceIdentityStatus.Enabled);

            var iterator1 = new Mock<IServiceIdentitiesIterator>();
            iterator1.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator1.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_initial,
                        si2,
                        si3
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator1.Object);

            var updatedIdentities = new List<ServiceIdentity>();
            var removedIdentities = new List<string>();
            IList<string> entireCache = new List<string>();

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);
            deviceScopeIdentitiesCache.ServiceIdentitiesUpdated += (sender, serviceIdentities) => entireCache = serviceIdentities;

            // Wait for initial refresh to complete
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));

            // Setup updated response
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == id1), It.IsAny<string>())).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == id2), It.IsAny<string>())).ReturnsAsync(Option.None<ServiceIdentity>());
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == id3), It.IsAny<string>())).ReturnsAsync(Option.None<ServiceIdentity>());

            // Refresh the authchain
            await deviceScopeIdentitiesCache.RefreshAuthChain($"{id3};{id2};{id1}");

            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity(id1);
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity(id2);
            Option<ServiceIdentity> receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity(id3);
            Option<string> authchain2 = await deviceScopeIdentitiesCache.GetAuthChain(id2);
            Option<string> authchain3 = await deviceScopeIdentitiesCache.GetAuthChain(id3);

            // Assert
            Assert.True(si1_updated.Equals(receivedServiceIdentity1.OrDefault()));
            Assert.False(receivedServiceIdentity2.HasValue);
            Assert.False(receivedServiceIdentity3.HasValue);
            Assert.False(authchain2.HasValue);
            Assert.False(authchain3.HasValue);
            Assert.Equal(1, entireCache.Count);
            Assert.Contains(si1_updated.Id, entireCache);
        }

        [Fact]
        public async Task RefreshIdentityNegativeCachingTest()
        {
            // Arrange
            int refreshDelaySec = 10;
            var store = GetEntityStore("cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            string id1 = "d1";
            var si1_initial = new ServiceIdentity(id1, null, "d1_scope", Enumerable.Empty<string>(), "1234", new List<string>() { Constants.IotEdgeIdentityCapability }, serviceAuthenticationSas, ServiceIdentityStatus.Enabled);
            var si1_updated = new ServiceIdentity(id1, null, "d1_scope", Enumerable.Empty<string>(), "4321", new List<string>() { Constants.IotEdgeIdentityCapability }, serviceAuthenticationNone, ServiceIdentityStatus.Disabled);

            var iterator1 = new Mock<IServiceIdentitiesIterator>();
            iterator1.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator1.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_initial,
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator1.Object);
            serviceProxy.SetupSequence(s => s.GetServiceIdentity(It.Is<string>(id => id == id1), It.IsAny<string>()))
                .ReturnsAsync(Option.Some(si1_initial))
                .ReturnsAsync(Option.Some(si1_updated));

            // Act
            var updatedIdentities = new List<ServiceIdentity>();
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(refreshDelaySec));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            // Wait for initial refresh to complete
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromSeconds(refreshDelaySec - 1));

            // Refresh the identity to trigger the delay
            await deviceScopeIdentitiesCache.RefreshServiceIdentity(id1);
            Option<ServiceIdentity> initialServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(id1);

            // Wait for delay to expire and try again
            await Task.Delay(TimeSpan.FromSeconds(refreshDelaySec + 1));
            await deviceScopeIdentitiesCache.RefreshServiceIdentity(id1);
            var updatedServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(id1);

            // Should be the same as initial value, as was still in the delay period
            Assert.Equal(si1_initial, initialServiceIdentity.OrDefault());
            Assert.Equal(si1_updated, updatedServiceIdentity.OrDefault());
        }

        [Fact]
        public async Task RefreshCacheNegativeCachingTest()
        {
            // Arrange
            int refreshDelaySec = 10;
            var store = GetEntityStore("cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            string id1 = "d1";
            var si1_initial = new ServiceIdentity(id1, null, "d1_scope", Enumerable.Empty<string>(), "1234", new List<string>() { Constants.IotEdgeIdentityCapability }, serviceAuthenticationSas, ServiceIdentityStatus.Enabled);
            var si1_updated = new ServiceIdentity(id1, null, "d1_scope", Enumerable.Empty<string>(), "4321", new List<string>() { Constants.IotEdgeIdentityCapability }, serviceAuthenticationNone, ServiceIdentityStatus.Disabled);

            var iterator = new Mock<IServiceIdentitiesIterator>();
            iterator.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator.Setup(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_initial,
                    });

            var iterator_updated = new Mock<IServiceIdentitiesIterator>();
            iterator_updated.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator_updated.Setup(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_updated,
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.SetupSequence(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator.Object)
                .Returns(iterator_updated.Object);

            // Act
            var updatedIdentities = new List<ServiceIdentity>();
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(refreshDelaySec));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);

            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));
            Option<ServiceIdentity> duringDelayReceivedServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(id1);

            // Wait for delay to expire and try again
            await Task.Delay(TimeSpan.FromSeconds(refreshDelaySec + 1));
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));
            var afterDelayReceivedServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(id1);

            Assert.Equal(si1_initial, duringDelayReceivedServiceIdentity.OrDefault());
            Assert.Equal(si1_updated, afterDelayReceivedServiceIdentity.OrDefault());
        }

        [Fact]
        public async Task RefreshCacheCycle_EmptyStore_ShouldRetry()
        {
            // Arrange
            var store = GetEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string leafId = "leaf";
            string leaf2Id = "leaf2";
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);
            var leaf2 = new ServiceIdentity(leaf2Id, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();
            serviceIdentityHierarchy.SetupSequence(s => s.Get(leafId))
                .ReturnsAsync(Option.None<ServiceIdentity>())
                .ReturnsAsync(Option.Some(leaf))
                .ReturnsAsync(Option.Some(leaf));
            serviceIdentityHierarchy.Setup(s => s.GetAllIds()).ReturnsAsync(new List<string>());
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns("edge");

            var failingServiceIdentityIterator = new Mock<IServiceIdentitiesIterator>();
            failingServiceIdentityIterator.Setup(s => s.HasNext).Returns(true);
            failingServiceIdentityIterator.Setup(s => s.GetNext())
                .ThrowsAsync(new Exception());

            var serviceIdentityIterator = new Mock<IServiceIdentitiesIterator>();
            serviceIdentityIterator.SetupSequence(s => s.HasNext)
                .Returns(true)
                .Returns(false);
            serviceIdentityIterator.Setup(s => s.GetNext())
                .ReturnsAsync(new List<ServiceIdentity>() { leaf });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.SetupSequence(s => s.GetServiceIdentitiesIterator())
                .Returns(failingServiceIdentityIterator.Object)
                .Returns(failingServiceIdentityIterator.Object)
                .Returns(serviceIdentityIterator.Object);
            serviceProxy.Setup(s => s.GetServiceIdentity(leaf2Id, It.IsAny<string>())).ReturnsAsync(Option.Some(leaf2));

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0));

            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromSeconds(20));

            // Act
            var leaf1ServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(leafId);
            // after cache cycle was initialized should allow refresh single identity
            await deviceScopeIdentitiesCache.RefreshServiceIdentity(leaf2Id);

            // Assert
            Assert.Equal(ServiceIdentityStatus.Enabled, leaf1ServiceIdentity.OrDefault().Status);
            Assert.Equal(leaf, leaf1ServiceIdentity.OrDefault());
            serviceIdentityHierarchy.VerifyAll();
            serviceProxy.Verify(s => s.GetServiceIdentity(leafId, It.IsAny<string>()), Times.Never);
            serviceProxy.Verify(s => s.GetServiceIdentity(leaf2Id, It.IsAny<string>()), Times.Once);
            serviceProxy.Verify(s => s.GetServiceIdentitiesIterator(), Times.Exactly(3));
        }

        [Fact]
        public async Task ReadFromCache_WithEmptyEdgeDeviceScope_ShouldRestore()
        {
            // Arrange
            string edgeDeviceId = "edge";
            string leafId = "leaf1";
            var store = await GetEntityStoreVersion1_1("cache", edgeDeviceId, leafId);

            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(ServiceAuthenticationType.None);

            string deviceScope = "ms-azure-iot-edge://edge-1234";
            var leaf = new ServiceIdentity(leafId, null, deviceScope, new List<string> { deviceScope }, "12345", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);
            var module = new ServiceIdentity(edgeDeviceId, "m1", null, new List<string> { }, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);

            var serviceIdentityIterator = new Mock<IServiceIdentitiesIterator>();
            serviceIdentityIterator.Setup(s => s.HasNext)
                .Returns(false);

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree(edgeDeviceId), Mock.Of<IServiceProxy>(), store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0));

            // Act
            var leaf1ServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(leafId);
            var moduleServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(module.Id);

            // Assert
            Assert.Equal(leaf, leaf1ServiceIdentity.OrDefault());
            Assert.Equal(module, moduleServiceIdentity.OrDefault());
        }

        [Fact]
        public async Task RefreshCacheCycle_PopulatedStore_ShouldNotRetry()
        {
            // Arrange
            var store = await GetPopulatedEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string leafId = "leaf";
            string leaf2Id = "leaf2";
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);
            var leaf2 = new ServiceIdentity(leaf2Id, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();
            serviceIdentityHierarchy.SetupSequence(s => s.Get(leafId))
                .ReturnsAsync(Option.None<ServiceIdentity>())
                .ReturnsAsync(Option.Some(leaf));
            serviceIdentityHierarchy.SetupSequence(s => s.Get(leaf2Id))
                .ReturnsAsync(Option.None<ServiceIdentity>())
                .ReturnsAsync(Option.Some(leaf2));
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns("edge");

            var failingServiceIdentityIterator = new Mock<IServiceIdentitiesIterator>();
            failingServiceIdentityIterator.Setup(s => s.HasNext).Returns(true);
            failingServiceIdentityIterator.Setup(s => s.GetNext())
                .ThrowsAsync(new Exception());

            var serviceIdentityIterator = new Mock<IServiceIdentitiesIterator>();
            serviceIdentityIterator.Setup(s => s.HasNext)
                .Returns(true);
            serviceIdentityIterator.Setup(s => s.GetNext())
                .ReturnsAsync(new List<ServiceIdentity>() { leaf });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.SetupSequence(s => s.GetServiceIdentitiesIterator())
                .Returns(failingServiceIdentityIterator.Object)
                .Returns(serviceIdentityIterator.Object);
            serviceProxy.Setup(s => s.GetServiceIdentity(leaf2Id, It.IsAny<string>())).ReturnsAsync(Option.Some(leaf2));

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromSeconds(5));

            // after cache cycle was initialized should allow refresh single identity
            await deviceScopeIdentitiesCache.RefreshServiceIdentity(leafId);
            var leaf1ServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(leafId);
            // after cache cycle was initialized should allow refresh single identity
            await deviceScopeIdentitiesCache.RefreshServiceIdentity(leaf2Id);

            // Assert
            Assert.Equal(ServiceIdentityStatus.Enabled, leaf1ServiceIdentity.OrDefault().Status);
            Assert.Equal(leaf, leaf1ServiceIdentity.OrDefault());
            serviceIdentityHierarchy.VerifyAll();
            serviceProxy.Verify(s => s.GetServiceIdentity(leafId, It.IsAny<string>()), Times.Once);
            serviceProxy.Verify(s => s.GetServiceIdentity(leaf2Id, It.IsAny<string>()), Times.Once);
            serviceProxy.Verify(s => s.GetServiceIdentitiesIterator(), Times.Once);
            serviceIdentityIterator.Verify(s => s.HasNext, Times.Never);
        }

        [Fact]
        public async Task RefreshServiceIdentityState_FromService_WithCacheNotInitiliazed_ShouldNotRefresh()
        {
            // Arrange
            var store = GetEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string leafId = "leaf";
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Disabled);
            var updatedLeaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();
            serviceIdentityHierarchy.SetupSequence(s => s.Get(leafId))
                .ReturnsAsync(Option.Some(leaf))
                .ReturnsAsync(Option.Some(updatedLeaf));
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns("edge");

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentity(leafId, It.IsAny<string>())).ReturnsAsync(Option.Some(updatedLeaf));

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act
            await deviceScopeIdentitiesCache.RefreshServiceIdentity(leafId);
            var updatedServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(leafId);

            // Assert
            Assert.Equal(ServiceIdentityStatus.Disabled, updatedServiceIdentity.OrDefault().Status);
            Assert.Equal(leaf, updatedServiceIdentity.OrDefault());
            Assert.NotEqual(updatedLeaf, updatedServiceIdentity.OrDefault());
            serviceIdentityHierarchy.VerifyAll();
            serviceProxy.Verify(s => s.GetServiceIdentity(leafId, It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RefreshServiceIdentityState_FromService_WithCacheInitiliazed_ShouldRefresh()
        {
            // Arrange
            var store = await GetPopulatedEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string leafId = "leaf";
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Disabled);
            var updatedLeaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();
            serviceIdentityHierarchy.SetupSequence(s => s.Get(leafId))
                .ReturnsAsync(Option.Some(leaf))
                .ReturnsAsync(Option.Some(updatedLeaf));
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns("edge");

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentity(leafId, It.IsAny<string>())).ReturnsAsync(Option.Some(updatedLeaf));

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act
            await deviceScopeIdentitiesCache.RefreshServiceIdentity(leafId);
            var updatedServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(leafId);

            // Assert
            Assert.Equal(ServiceIdentityStatus.Enabled, updatedServiceIdentity.OrDefault().Status);
            Assert.Equal(updatedLeaf, updatedServiceIdentity.OrDefault());
            Assert.NotEqual(leaf, updatedServiceIdentity.OrDefault());
            serviceIdentityHierarchy.VerifyAll();
            serviceProxy.Verify(s => s.GetServiceIdentity(leafId, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task VerifyServiceIdentityState_FromService_WithException()
        {
            // Arrange
            var store = await GetPopulatedEntityStore("cache");

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentity("d2", It.IsAny<string>())).ThrowsAsync(new DeviceInvalidStateException("Device is out of scope."));
            serviceProxy.Setup(s => s.GetServiceIdentity("d2", "m1", It.IsAny<string>())).ThrowsAsync(new DeviceInvalidStateException("Device is out of scope."));

            DeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromMinutes(2));

            // Act
            var deviceInvalidStateException = await Assert.ThrowsAsync<DeviceInvalidStateException>(() => deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState("d2", false, true));
            var moduleInvalidStateException = await Assert.ThrowsAsync<DeviceInvalidStateException>(() => deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState("d2/m1", false, true));

            // Assert
            Assert.Contains("Device is out of scope.", deviceInvalidStateException.Message);
            Assert.Contains("Device is out of scope.", moduleInvalidStateException.Message);
            serviceProxy.VerifyAll();
        }

        [Fact]
        public async Task VerifyServiceIdentityState_FromService_WithDisabled()
        {
            // Arrange
            var store = await GetPopulatedEntityStore("cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            var si_device = new ServiceIdentity("d2", "1234", new List<string>() { Constants.IotEdgeIdentityCapability }, serviceAuthenticationNone, ServiceIdentityStatus.Disabled);
            var si_module = new ServiceIdentity("d2", "m1", "e1", Enumerable.Empty<string>(), "2345", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Disabled);

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentity("d2", It.IsAny<string>())).ReturnsAsync(Option.Some(si_device));
            serviceProxy.Setup(s => s.GetServiceIdentity("d2", "m1", It.IsAny<string>())).ReturnsAsync(Option.Some(si_module));

            DeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("d2"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromMinutes(2));

            // Act
            var deviceInvalidStateException = await Assert.ThrowsAsync<DeviceInvalidStateException>(() => deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState("d2", false, true));
            var moduleInvalidStateException = await Assert.ThrowsAsync<DeviceInvalidStateException>(() => deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState("d2/m1", false, true));

            // Assert
            Assert.Contains("Device is disabled.", deviceInvalidStateException.Message);
            Assert.Contains("Device is disabled.", moduleInvalidStateException.Message);
            serviceProxy.VerifyAll();
        }

        [Fact]
        public async Task VerifyServiceIdentityState_FromService_WithRemovedFromScopeTest()
        {
            // Arrange
            var refreshDelay = TimeSpan.FromSeconds(2);
            var store = await GetPopulatedEntityStore("cache");
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            var si_device = new ServiceIdentity("d2", "1234", new List<string>() { Constants.IotEdgeIdentityCapability }, serviceAuthenticationSas, ServiceIdentityStatus.Enabled);
            var si_module = new ServiceIdentity("d2", "m1", "e1", Enumerable.Empty<string>(), "1234", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentity("d2", It.IsAny<string>())).ReturnsAsync(Option.Some(si_device));
            serviceProxy.Setup(s => s.GetServiceIdentity("d2", "m1", It.IsAny<string>())).ReturnsAsync(Option.Some(si_module));

            DeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("d2"), serviceProxy.Object, store, TimeSpan.FromHours(1), refreshDelay);

            await deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState("d2", false, true);
            await deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState("d2/m1", false, true);

            serviceProxy.Setup(s => s.GetServiceIdentity("d2", It.IsAny<string>())).ThrowsAsync(new DeviceInvalidStateException("Device is out of scope."));
            serviceProxy.Setup(s => s.GetServiceIdentity("d2", "m1", It.IsAny<string>())).ThrowsAsync(new DeviceInvalidStateException("Device is out of scope."));

            await deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState("d2", false, true);
            await deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState("d2/m1", false, true);

            // Act
            await Task.Delay(refreshDelay + TimeSpan.FromSeconds(5));
            var deviceInvalidStateException = await Assert.ThrowsAsync<DeviceInvalidStateException>(() => deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState("d2", false, true));
            var moduleInvalidStateException = await Assert.ThrowsAsync<DeviceInvalidStateException>(() => deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState("d2/m1", false, true));

            // Assert
            Assert.Contains("Device is out of scope.", deviceInvalidStateException.Message);
            Assert.Contains("Device is out of scope.", moduleInvalidStateException.Message);
            serviceProxy.VerifyAll();
        }

        [Fact]
        public async Task VerifyServiceIdentityAuthChain_Enabled_ShouldSucceed()
        {
            // Arrange
            var store = GetEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string parentEdgeId = "parentEdge";
            string childEdgeId = "childEdge";
            string leafId = "leaf";
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);
            string authChain = leafId + ";" + childEdgeId + ";" + parentEdgeId;

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();
            serviceIdentityHierarchy.Setup(s => s.Get(leafId)).ReturnsAsync(Option.Some(leaf));
            serviceIdentityHierarchy.Setup(s => s.TryGetAuthChain(leafId)).ReturnsAsync(Try.Success(authChain));
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns(parentEdgeId);

            var identitiesIterator = new Mock<IServiceIdentitiesIterator>();
            identitiesIterator.Setup(i => i.HasNext).Returns(false);
            var serviceProxy = new Mock<IServiceProxy>();

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act
            var authChainActual = await deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState(leafId, true, false);

            // Assert
            Assert.Equal(authChain, authChainActual);
            serviceIdentityHierarchy.VerifyAll();
            serviceProxy.VerifyAll();
        }

        [Fact]
        public async Task VerifyServiceIdentityAuthChain_Enabled_DisabledAfterRefresh_ShouldThrow()
        {
            // Arrange
            var store = await GetPopulatedEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string parentEdgeId = "parentEdge";
            string childEdgeId = "childEdge";
            string leafId = "leaf";
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);
            var updatedLeaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Disabled);
            string authChain = leafId + ";" + childEdgeId + ";" + parentEdgeId;

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();

            serviceIdentityHierarchy.SetupSequence(s => s.Get(leafId))
                .ReturnsAsync(Option.Some(leaf))
                .ReturnsAsync(Option.Some(updatedLeaf));
            serviceIdentityHierarchy.Setup(s => s.TryGetAuthChain(leafId)).ReturnsAsync(Try.Success(authChain));
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns(parentEdgeId);

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentity(leafId, It.Is<string>(id => id == childEdgeId))).ReturnsAsync(Option.Some(updatedLeaf));

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act / Assert
            var ex = await Assert.ThrowsAsync<DeviceInvalidStateException>(() => deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState(leafId, true, true));
            Assert.Contains("Device is disabled.", ex.Message);
            serviceIdentityHierarchy.VerifyAll();
            serviceProxy.VerifyAll();
        }

        [Fact]
        public async Task VerifyServiceIdentityAuthChain_Disabled_ShouldThrow()
        {
            // Arrange
            var store = GetEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string parentEdgeId = "parentEdge";
            string childEdgeId = "childEdge";
            string leafId = "leaf";
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Disabled);
            string authChain = leafId + ";" + childEdgeId + ";" + parentEdgeId;

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();
            serviceIdentityHierarchy.Setup(s => s.Get(leafId)).ReturnsAsync(Option.Some(leaf));
            serviceIdentityHierarchy.Setup(s => s.TryGetAuthChain(leafId)).ReturnsAsync(Try.Success(authChain));
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns(parentEdgeId);

            var serviceProxy = new Mock<IServiceProxy>();

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act / Assert
            var ex = await Assert.ThrowsAsync<DeviceInvalidStateException>(() => deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState(leafId, true, false));
            Assert.Contains("Device is disabled.", ex.Message);
            serviceIdentityHierarchy.VerifyAll();
            serviceProxy.VerifyAll();
        }

        [Fact]
        public async Task VerifyServiceIdentityAuthChain_Disabled_EnabledAfterRefresh_ShouldSucceed()
        {
            // Arrange
            var store = await GetPopulatedEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string parentEdgeId = "parentEdge";
            string childEdgeId = "childEdge";
            string leafId = "leaf";
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Disabled);
            var updatedLeaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);
            string authChain = leafId + ";" + childEdgeId + ";" + parentEdgeId;

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();
            serviceIdentityHierarchy.SetupSequence(s => s.Get(leafId))
                .ReturnsAsync(Option.Some(leaf))
                .ReturnsAsync(Option.Some(updatedLeaf));
            serviceIdentityHierarchy.Setup(s => s.TryGetAuthChain(leafId)).ReturnsAsync(Try.Success(authChain));
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns(parentEdgeId);

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentity(leafId, It.Is<string>(id => id == childEdgeId))).ReturnsAsync(Option.Some(updatedLeaf));

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act
            var authChainActual = await deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState(leafId, true, true);

            // Assert
            Assert.Equal(authChain, authChainActual);
            serviceIdentityHierarchy.VerifyAll();
            serviceProxy.VerifyAll();
        }

        [Fact]
        public async Task VerifyServiceIdentityAuthChain_Disabled_EnabledAfterRefresh_ObBehalfOfEdgeDevice_ShouldSucceed()
        {
            // Arrange
            var store = await GetPopulatedEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string leafId = "leaf";
            string edgeDeviceId = "edgeDeviceId";
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Disabled);
            var updatedLeaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);
            string authChain = leafId;

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();
            serviceIdentityHierarchy.SetupSequence(s => s.Get(leafId))
                .ReturnsAsync(Option.Some(leaf))
                .ReturnsAsync(Option.Some(updatedLeaf));
            serviceIdentityHierarchy.Setup(s => s.TryGetAuthChain(leafId)).ReturnsAsync(Try.Success(authChain));
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns(edgeDeviceId);

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentity(leafId, It.Is<string>(id => id == edgeDeviceId))).ReturnsAsync(Option.Some(updatedLeaf));

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act
            var authChainActual = await deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState(leafId, true, true);

            // Assert
            Assert.Equal(authChain, authChainActual);
            serviceIdentityHierarchy.VerifyAll();
            serviceProxy.VerifyAll();
        }

        [Fact]
        public async Task VerifyServiceIdentityAuthChain_NoAuthChain_ShouldThrow()
        {
            // Arrange
            var store = GetEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string parentEdgeId = "parentEdge";
            string childEdgeId = "childEdge";
            string leafId = "leaf";
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);
            string authChain = leafId + ";" + childEdgeId + ";" + parentEdgeId;

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns(parentEdgeId);

            var leafInHierarchy = Option.None<ServiceIdentity>();
            serviceIdentityHierarchy.Setup(s => s.TryGetAuthChain(leafId)).ReturnsAsync(Try<string>.Failure(new DeviceInvalidStateException("Device is out of scope.")));

            var serviceProxy = new Mock<IServiceProxy>();

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act / Assert
            var ex = await Assert.ThrowsAsync<DeviceInvalidStateException>(() => deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState(leafId, true, false));
            Assert.Contains("Device is out of scope.", ex.Message);
            serviceIdentityHierarchy.VerifyAll();
            serviceProxy.VerifyAll();
        }

        [Fact]
        public async Task VerifyServiceIdentityAuthChain_Enabled_NoAuthChainAfterRefresh_ShouldThrow()
        {
            // Arrange
            var store = await GetPopulatedEntityStore("cache");
            List<string> edgeCapability = new List<string>() { Constants.IotEdgeIdentityCapability };
            var serviceAuth = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));
            string parentEdgeId = "parentEdge";
            string childEdgeId = "childEdge";
            string leafId = "leaf";
            var leaf = new ServiceIdentity(leafId, "1234", Enumerable.Empty<string>(), serviceAuth, ServiceIdentityStatus.Enabled);
            string authChain = leafId + ";" + childEdgeId + ";" + parentEdgeId;

            var serviceIdentityHierarchy = new Mock<IServiceIdentityHierarchy>();

            var leafInHierarchy = Option.Some(leaf);
            serviceIdentityHierarchy.Setup(s => s.Get(leafId)).ReturnsAsync(leafInHierarchy);
            serviceIdentityHierarchy.SetupSequence(s => s.TryGetAuthChain(leafId)).ReturnsAsync(Try.Success(authChain)).ReturnsAsync(Try<string>.Failure(new DeviceInvalidStateException("Device is out of scope.")));
            serviceIdentityHierarchy.Setup(s => s.GetActorDeviceId()).Returns(parentEdgeId);

            var serviceProxy = new Mock<IServiceProxy>();

            var deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceIdentityHierarchy.Object, serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act / Assert
            var ex = await Assert.ThrowsAsync<DeviceInvalidStateException>(() => deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState(leafId, true, true));
            Assert.Contains("Device is out of scope.", ex.Message);
            serviceIdentityHierarchy.VerifyAll();
            serviceProxy.VerifyAll();
        }

        static string GetKey() => Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));

        static IEntityStore<string, string> GetEntityStore(string entityName)
            => new EntityStore<string, string>(new KeyValueStoreMapper<string, byte[], string, byte[]>(new InMemoryDbStore(), new BytesMapper<string>(), new BytesMapper<string>()), entityName);

        static async Task<IEntityStore<string, string>> GetPopulatedEntityStore(string entityName)
        {
            var store = GetEntityStore(entityName);
            var serviceAuthentication = new ServiceAuthentication(ServiceAuthenticationType.None);
            var si1 = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            var storedSi1 = new DeviceScopeIdentitiesCache.StoredServiceIdentity(si1);
            await store.Put(si1.Id, storedSi1.ToJson());

            return store;
        }

        static async Task<IEntityStore<string, string>> GetEntityStoreVersion1_1(string entityName, string edgeDeviceId, string leafId)
        {
            var store = GetEntityStore(entityName);
            string storedEdge = "{\"serviceIdentity\":{\"deviceId\":\"" + edgeDeviceId + "\",\"moduleId\":null,\"capabilities\":[\"iotEdge\"],\"authentication\":{\"type\":\"None\",\"symmetricKey\":null,\"x509Thumbprint\":null},\"status\":\"enabled\",\"generationId\":\"1234\"},\"id\":\"d1\",\"timestamp\":\"2021-04-14T22:24:52.1985176Z\"}";
            await store.Put(edgeDeviceId, storedEdge);

            string storedModule = "{\"serviceIdentity\":{\"deviceId\":\"" + edgeDeviceId + "\",\"moduleId\":\"m1\",\"capabilities\":[],\"authentication\":{\"type\":\"None\",\"symmetricKey\":null,\"x509Thumbprint\":null},\"status\":\"enabled\",\"generationId\":\"1234\"},\"id\":\"d1\",\"timestamp\":\"2021-04-14T22:24:52.1985176Z\"}";
            await store.Put($"{edgeDeviceId}/m1", storedModule);

            string storedLeaf = "{\"serviceIdentity\":{\"deviceId\":\"" + leafId + "\",\"moduleId\":null,\"capabilities\":[],\"authentication\":{\"type\":\"None\",\"symmetricKey\":null,\"x509Thumbprint\":null},\"status\":\"enabled\",\"generationId\":\"12345\"},\"id\":\"d1\",\"timestamp\":\"2021-04-14T22:24:52.1985176Z\"}";
            await store.Put(leafId, storedLeaf);

            return store;
        }
    }
}
