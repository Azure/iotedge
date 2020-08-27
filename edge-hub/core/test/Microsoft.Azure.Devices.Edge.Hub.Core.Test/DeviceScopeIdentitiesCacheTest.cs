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
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(0));
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

            // Wait for another refresh cycle to complete
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));

            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m1");
            receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");
            receivedServiceIdentity4 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2/m4");

            // Assert
            Assert.True(si1().Equals(receivedServiceIdentity1.OrDefault()));
            Assert.True(si2().Equals(receivedServiceIdentity2.OrDefault()));
            Assert.True(si3().Equals(receivedServiceIdentity3.OrDefault()));
            Assert.False(receivedServiceIdentity4.HasValue);

            Assert.Empty(updatedIdentities);
            Assert.Single(removedIdentities);
            Assert.Contains("d2/m4", removedIdentities);
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
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d1"))).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d2"))).ReturnsAsync(Option.None<ServiceIdentity>());

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
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d1"))).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d2"))).ReturnsAsync(Option.None<ServiceIdentity>());
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d3"))).ReturnsAsync(Option.None<ServiceIdentity>());

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
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d1"), It.Is<string>(id => id == "m1"))).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d2"), It.Is<string>(id => id == "m2"))).ReturnsAsync(Option.None<ServiceIdentity>());

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
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d1"))).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d2"))).ReturnsAsync(Option.None<ServiceIdentity>());
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == "d3"))).ReturnsAsync(Option.Some(si3));

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
            serviceProxy.Setup(s => s.GetServiceIdentity("d1", "m1")).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity("d2", "m2")).ReturnsAsync(Option.None<ServiceIdentity>());
            serviceProxy.Setup(s => s.GetServiceIdentity("d3", "m3")).ReturnsAsync(Option.Some(si3));

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
            serviceProxy.Setup(s => s.GetServiceIdentity("d2")).ReturnsAsync(Option.Some(si_device));
            serviceProxy.Setup(s => s.GetServiceIdentity("d1", "m1")).ReturnsAsync(Option.Some(si_module));

            DeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(0));

            // Act
            Option<ServiceIdentity> deviceServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentityFromService("d2");
            Option<ServiceIdentity> moduleServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentityFromService("d1/m1");

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
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == id1))).ReturnsAsync(Option.Some(si1_updated));
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == id2))).ReturnsAsync(Option.None<ServiceIdentity>());
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == id3))).ReturnsAsync(Option.None<ServiceIdentity>());

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

        [Fact(Skip = "Flakey in pipeline but passes locally")]
        public async Task RefreshIdentityNegativeCachingTest()
        {
            // Arrange
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
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == id1))).ReturnsAsync(Option.Some(si1_initial));

            // Act
            int refreshDelaySec = 10;
            var updatedIdentities = new List<ServiceIdentity>();
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(refreshDelaySec));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);

            // Wait for initial refresh to complete
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));

            // Refresh the identity to trigger the delay
            await deviceScopeIdentitiesCache.RefreshServiceIdentity(id1);

            // Setup updated response
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == id1))).ReturnsAsync(Option.Some(si1_updated));

            // Refresh again without waiting
            await deviceScopeIdentitiesCache.RefreshServiceIdentity(id1);
            Option<ServiceIdentity> receivedServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(id1);

            // Should be the same as initial value, as we're still in the delay period
            Assert.True(si1_initial.Equals(receivedServiceIdentity.OrDefault()));

            // Wait for delay to expire and try again
            await Task.Delay(TimeSpan.FromSeconds(refreshDelaySec));
            await deviceScopeIdentitiesCache.RefreshServiceIdentity(id1);
            receivedServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(id1);

            // Should be updated now
            Assert.True(si1_updated.Equals(receivedServiceIdentity.OrDefault()));

            // Flip the response back again and refresh again without waiting
            serviceProxy.Setup(s => s.GetServiceIdentity(It.Is<string>(id => id == id1))).ReturnsAsync(Option.Some(si1_initial));
            await deviceScopeIdentitiesCache.RefreshServiceIdentity(id1);
            receivedServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(id1);

            // Refresh delay should have been ignored due to AuthenticationType.None
            Assert.True(si1_initial.Equals(receivedServiceIdentity.OrDefault()));
        }

        [Fact]
        public async Task RefreshCacheNegativeCachingTest()
        {
            // Arrange
            var store = GetEntityStore("cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            string id1 = "d1";
            var si1_initial = new ServiceIdentity(id1, null, "d1_scope", Enumerable.Empty<string>(), "1234", new List<string>() { Constants.IotEdgeIdentityCapability }, serviceAuthenticationSas, ServiceIdentityStatus.Enabled);
            var si1_updated = new ServiceIdentity(id1, null, "d1_scope", Enumerable.Empty<string>(), "4321", new List<string>() { Constants.IotEdgeIdentityCapability }, serviceAuthenticationNone, ServiceIdentityStatus.Disabled);

            var iterator = new Mock<IServiceIdentitiesIterator>();
            iterator.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false)
                .Returns(true)
                .Returns(false);
            iterator.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_initial,
                    });

            var iterator_updated = new Mock<IServiceIdentitiesIterator>();
            iterator_updated.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator_updated.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_updated,
                    })
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1_updated,
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.Setup(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator.Object);

            // Act
            int refreshDelaySec = 10;
            var updatedIdentities = new List<ServiceIdentity>();
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(new ServiceIdentityTree("deviceId"), serviceProxy.Object, store, TimeSpan.FromHours(1), TimeSpan.FromSeconds(refreshDelaySec));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);

            // Wait for initial refresh to complete
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));

            // Setup the updated response
            serviceProxy.Setup(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator_updated.Object);

            // Trigger another refresh without waiting
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));
            Option<ServiceIdentity> receivedServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(id1);

            // Should be the same as initial value, as we're still in the delay period
            Assert.True(si1_initial.Equals(receivedServiceIdentity.OrDefault()));

            // Wait for delay to expire and try again
            await Task.Delay(TimeSpan.FromSeconds(refreshDelaySec));
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            await deviceScopeIdentitiesCache.WaitForCacheRefresh(TimeSpan.FromMinutes(1));
            receivedServiceIdentity = await deviceScopeIdentitiesCache.GetServiceIdentity(id1);

            // Should be updated now
            Assert.True(si1_updated.Equals(receivedServiceIdentity.OrDefault()));
        }

        static string GetKey() => Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));

        static IEntityStore<string, string> GetEntityStore(string entityName)
            => new EntityStore<string, string>(new KeyValueStoreMapper<string, byte[], string, byte[]>(new InMemoryDbStore(), new BytesMapper<string>(), new BytesMapper<string>()), entityName);
    }
}
