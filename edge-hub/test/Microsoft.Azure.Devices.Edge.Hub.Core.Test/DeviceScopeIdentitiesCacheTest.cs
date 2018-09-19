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

            var store = new EntityStore<string, string>(new InMemoryDbStore(), "cache");
            var serviceAuthentication = new ServiceAuthentication(ServiceAuthenticationType.None);
            var si1 = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            var si2 = new ServiceIdentity("d2", "m1", "2345", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            var storedSi1 = new DeviceScopeIdentitiesCache.StoredServiceIdentity(si1);
            await store.Put(si1.Id, storedSi1.ToJson());
            var storedSi2 = new DeviceScopeIdentitiesCache.StoredServiceIdentity(si2);
            await store.Put(si2.Id, storedSi2.ToJson());

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceProxy.Object, store, TimeSpan.FromHours(1));
            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m1");

            // Assert
            CompareServiceIdentities(si1, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);
        }

        [Fact]
        public async Task RefreshCacheTest()
        {
            // Arrange            
            var store = new EntityStore<string, string>(new InMemoryDbStore(), "cache");
            var serviceAuthentication = new ServiceAuthentication(ServiceAuthenticationType.None);
            var si1 = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            var si2 = new ServiceIdentity("d2", "m1", "2345", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);

            var si3 = new ServiceIdentity("d3", "5678", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            var si4 = new ServiceIdentity("d2", "m4", "9898", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);

            var iterator1 = new Mock<IServiceIdentitiesIterator>();
            iterator1.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(true)
                .Returns(false);
            iterator1.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1,
                        si2
                    })
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si3,
                        si4
                    });

            var iterator2 = new Mock<IServiceIdentitiesIterator>();
            iterator2.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator2.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1,
                        si2,
                        si3
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.SetupSequence(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator1.Object)
                .Returns(iterator2.Object);
            var updatedIdentities = new List<ServiceIdentity>();
            var removedIdentities = new List<string>();

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceProxy.Object, store, TimeSpan.FromSeconds(8));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);

            // Wait for refresh to complete
            await Task.Delay(TimeSpan.FromSeconds(3));
            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m1");
            Option<ServiceIdentity> receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");
            Option<ServiceIdentity> receivedServiceIdentity4 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m4");

            // Assert
            CompareServiceIdentities(si1, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);
            CompareServiceIdentities(si3, receivedServiceIdentity3);
            CompareServiceIdentities(si4, receivedServiceIdentity4);

            Assert.Equal(0, updatedIdentities.Count);
            Assert.Equal(0, removedIdentities.Count);

            // Wait for another refresh cycle to complete
            await Task.Delay(TimeSpan.FromSeconds(8));

            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m1");
            receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");
            receivedServiceIdentity4 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m4");

            // Assert
            CompareServiceIdentities(si1, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);
            CompareServiceIdentities(si3, receivedServiceIdentity3);
            Assert.False(receivedServiceIdentity4.HasValue);

            Assert.Equal(0, updatedIdentities.Count);
            Assert.Equal(1, removedIdentities.Count);
            Assert.True(removedIdentities.Contains("d2/m4"));
        }

        [Fact]
        public async Task RefreshCacheWithRefreshRequestTest()
        {
            // Arrange            
            var store = new EntityStore<string, string>(new InMemoryDbStore(), "cache");
            var serviceAuthentication = new ServiceAuthentication(ServiceAuthenticationType.None);
            var si1 = new ServiceIdentity("d1", "1234", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            var si2 = new ServiceIdentity("d2", "m1", "2345", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);

            var si3 = new ServiceIdentity("d3", "5678", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);
            var si4 = new ServiceIdentity("d2", "m4", "9898", Enumerable.Empty<string>(), serviceAuthentication, ServiceIdentityStatus.Enabled);

            var iterator1 = new Mock<IServiceIdentitiesIterator>();
            iterator1.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(true)
                .Returns(false);
            iterator1.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1,
                        si2
                    })
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si3,
                        si4
                    });

            var iterator2 = new Mock<IServiceIdentitiesIterator>();
            iterator2.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator2.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1,
                        si2,
                        si3
                    });

            var iterator3 = new Mock<IServiceIdentitiesIterator>();
            iterator3.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator3.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si1,
                        si2
                    });

            var iterator4 = new Mock<IServiceIdentitiesIterator>();
            iterator4.SetupSequence(i => i.HasNext)
                .Returns(true)
                .Returns(false);
            iterator4.SetupSequence(i => i.GetNext())
                .ReturnsAsync(
                    new List<ServiceIdentity>
                    {
                        si3,
                        si4
                    });

            var serviceProxy = new Mock<IServiceProxy>();
            serviceProxy.SetupSequence(s => s.GetServiceIdentitiesIterator())
                .Returns(iterator1.Object)
                .Returns(iterator2.Object)
                .Returns(iterator3.Object)
                .Returns(iterator4.Object);
            var updatedIdentities = new List<ServiceIdentity>();
            var removedIdentities = new List<string>();

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceProxy.Object, store, TimeSpan.FromSeconds(7));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);
            
            // Wait for refresh to complete
            await Task.Delay(TimeSpan.FromSeconds(3));
            
            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m1");
            Option<ServiceIdentity> receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");
            Option<ServiceIdentity> receivedServiceIdentity4 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m4");

            // Assert
            CompareServiceIdentities(si1, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);
            CompareServiceIdentities(si3, receivedServiceIdentity3);
            CompareServiceIdentities(si4, receivedServiceIdentity4);

            Assert.Equal(0, updatedIdentities.Count);
            Assert.Equal(0, removedIdentities.Count);

            // Act - Signal refresh cache multiple times. It should get picked up twice.
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            deviceScopeIdentitiesCache.InitiateCacheRefresh();
            deviceScopeIdentitiesCache.InitiateCacheRefresh();        

            // Wait for the 2 refresh cycles to complete, this time because of the refresh request
            await Task.Delay(TimeSpan.FromSeconds(2));
            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m1");
            receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");
            receivedServiceIdentity4 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m4");

            // Assert
            CompareServiceIdentities(si1, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);
            Assert.False(receivedServiceIdentity3.HasValue);
            Assert.False(receivedServiceIdentity4.HasValue);

            Assert.Equal(0, updatedIdentities.Count);
            Assert.Equal(2, removedIdentities.Count);
            Assert.True(removedIdentities.Contains("d2/m4"));
            Assert.True(removedIdentities.Contains("d3"));

            // Wait for another refresh cycle to complete, this time because timeout
            await Task.Delay(TimeSpan.FromSeconds(8));
            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m1");
            receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");
            receivedServiceIdentity4 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m4");

            // Assert
            CompareServiceIdentities(si3, receivedServiceIdentity3);
            CompareServiceIdentities(si4, receivedServiceIdentity4);
            Assert.False(receivedServiceIdentity1.HasValue);
            Assert.False(receivedServiceIdentity2.HasValue);

            Assert.Equal(0, updatedIdentities.Count);
            Assert.Equal(4, removedIdentities.Count);
            Assert.True(removedIdentities.Contains("d2/m1"));
            Assert.True(removedIdentities.Contains("d1"));
        }

        [Fact]
        public async Task RefreshServiceIdentityTest_Device()
        {
            // Arrange            
            var store = new EntityStore<string, string>(new InMemoryDbStore(), "cache");
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

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceProxy.Object, store, TimeSpan.FromHours(1));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);

            // Wait for refresh to complete
            await Task.Delay(TimeSpan.FromSeconds(3));
            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2");

            // Assert
            CompareServiceIdentities(si1_initial, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);

            // Update the identities
            await deviceScopeIdentitiesCache.RefreshServiceIdentity("d1");
            await deviceScopeIdentitiesCache.RefreshServiceIdentity("d2");

            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2");

            // Assert
            CompareServiceIdentities(si1_updated, receivedServiceIdentity1);
            Assert.False(receivedServiceIdentity2.HasValue);
            Assert.Equal(1, removedIdentities.Count);
            Assert.Equal("d2", removedIdentities[0]);
            Assert.Equal(1, updatedIdentities.Count);
            Assert.Equal("d1", updatedIdentities[0].Id);
        }

        [Fact]
        public async Task RefreshServiceIdentityTest_List()
        {
            // Arrange            
            var store = new EntityStore<string, string>(new InMemoryDbStore(), "cache");
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

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceProxy.Object, store, TimeSpan.FromHours(1));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);

            // Wait for refresh to complete
            await Task.Delay(TimeSpan.FromSeconds(3));
            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2");

            // Assert
            CompareServiceIdentities(si1_initial, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);

            // Update the identities
            await deviceScopeIdentitiesCache.RefreshServiceIdentities(new string[] { "d1", "d2" });

            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2");

            // Assert
            CompareServiceIdentities(si1_updated, receivedServiceIdentity1);
            Assert.False(receivedServiceIdentity2.HasValue);
            Assert.Equal(1, removedIdentities.Count);
            Assert.Equal("d2", removedIdentities[0]);
            Assert.Equal(1, updatedIdentities.Count);
            Assert.Equal("d1", updatedIdentities[0].Id);
        }

        [Fact]
        public async Task RefreshServiceIdentityTest_Module()
        {
            // Arrange            
            var store = new EntityStore<string, string>(new InMemoryDbStore(), "cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            var si1_initial = new ServiceIdentity("d1", "m1", "1234", Enumerable.Empty<string>(), serviceAuthenticationNone, ServiceIdentityStatus.Enabled);
            var si1_updated = new ServiceIdentity("d1", "m1", "1234", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);
            var si2 = new ServiceIdentity("d2", "m2", "5678", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);

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

            // Act
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceProxy.Object, store, TimeSpan.FromHours(1));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);

            // Wait for refresh to complete
            await Task.Delay(TimeSpan.FromSeconds(3));
            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1", "m1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m2");

            // Assert
            CompareServiceIdentities(si1_initial, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);

            // Update the identities
            await deviceScopeIdentitiesCache.RefreshServiceIdentity("d1", "m1");
            await deviceScopeIdentitiesCache.RefreshServiceIdentity("d2", "m2");

            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1", "m1");
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m2");

            // Assert
            CompareServiceIdentities(si1_updated, receivedServiceIdentity1);
            Assert.False(receivedServiceIdentity2.HasValue);
            Assert.Equal(1, removedIdentities.Count);
            Assert.Equal("d2/m2", removedIdentities[0]);
            Assert.Equal(1, updatedIdentities.Count);
            Assert.Equal("d1/m1", updatedIdentities[0].Id);
        }

        [Fact]
        public async Task GetServiceIdentityTest_Device()
        {
            // Arrange            
            var store = new EntityStore<string, string>(new InMemoryDbStore(), "cache");
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
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceProxy.Object, store, TimeSpan.FromHours(1));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);

            // Wait for refresh to complete
            await Task.Delay(TimeSpan.FromSeconds(3));

            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2");
            Option<ServiceIdentity> receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3");

            // Assert
            CompareServiceIdentities(si1_initial, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);
            Assert.False(receivedServiceIdentity3.HasValue);

            // Get the identities with refresh
            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1", true);
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", true);
            receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3", true);

            // Assert
            CompareServiceIdentities(si1_initial, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);
            CompareServiceIdentities(si3, receivedServiceIdentity3);
            Assert.Equal(0, removedIdentities.Count);
            Assert.Equal(0, updatedIdentities.Count);
        }

        [Fact]
        public async Task GetServiceIdentityTest_Module()
        {
            // Arrange            
            var store = new EntityStore<string, string>(new InMemoryDbStore(), "cache");
            var serviceAuthenticationNone = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceAuthenticationSas = new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey()));

            var si1_initial = new ServiceIdentity("d1", "m1", "1234", Enumerable.Empty<string>(), serviceAuthenticationNone, ServiceIdentityStatus.Enabled);
            var si1_updated = new ServiceIdentity("d1", "m1", "1234", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Disabled);
            var si2 = new ServiceIdentity("d2", "m2", "5678", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);
            var si3 = new ServiceIdentity("d3", "m3", "0987", Enumerable.Empty<string>(), serviceAuthenticationSas, ServiceIdentityStatus.Enabled);

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
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceProxy.Object, store, TimeSpan.FromHours(1));
            deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, identity) => updatedIdentities.Add(identity);
            deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, s) => removedIdentities.Add(s);

            // Wait for refresh to complete
            await Task.Delay(TimeSpan.FromSeconds(3));

            Option<ServiceIdentity> receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1", "m1");
            Option<ServiceIdentity> receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m2");
            Option<ServiceIdentity> receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3", "m3");

            // Assert
            CompareServiceIdentities(si1_initial, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);
            Assert.False(receivedServiceIdentity3.HasValue);

            // Get the identities with refresh
            receivedServiceIdentity1 = await deviceScopeIdentitiesCache.GetServiceIdentity("d1", "m1", true);
            receivedServiceIdentity2 = await deviceScopeIdentitiesCache.GetServiceIdentity("d2", "m2", true);
            receivedServiceIdentity3 = await deviceScopeIdentitiesCache.GetServiceIdentity("d3", "m3", true);

            // Assert
            CompareServiceIdentities(si1_initial, receivedServiceIdentity1);
            CompareServiceIdentities(si2, receivedServiceIdentity2);
            CompareServiceIdentities(si3, receivedServiceIdentity3);
            Assert.Equal(0, removedIdentities.Count);
            Assert.Equal(0, updatedIdentities.Count);
        }

        static void CompareServiceIdentities(ServiceIdentity si1, Option<ServiceIdentity> si2Option)
        {
            if (si1 == null)
            {
                Assert.False(si2Option.HasValue);
            }
            else
            {
                ServiceIdentity si2 = si2Option.OrDefault();
                Assert.Equal(si1.DeviceId, si2.DeviceId);
                Assert.Equal(si1.GenerationId, si2.GenerationId);
                Assert.Equal(si1.IsModule, si2.IsModule);
                if (si1.IsModule)
                {
                    Assert.Equal(si1.ModuleId.OrDefault(), si2.ModuleId.OrDefault());
                }
                Assert.Equal(si1.Capabilities.Count(), si2.Capabilities.Count());
                Assert.Equal(si1.Authentication.Type, si2.Authentication.Type);
            }
        }

        static string GetKey() => Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
    }
}
