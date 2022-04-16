// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Moq;
    using Xunit;

    [Unit]
    public class ConfigUpdaterTest
    {
        static readonly Dictionary<string, string> Routes = new Dictionary<string, string>
        {
            ["r1"] = "FROM /messages/* INTO $upstream",
            ["r2"] = "FROM /messages/modules* INTO $upstream",
            ["r3"] = "FROM /messages/modules/m1* INTO $upstream",
            ["r4"] = "FROM /messages/modules/m1/outputs/o1* INTO $upstream",
        };

        [Fact]
        public async Task TestInitialConfigUpdate_NoWaitForInit()
        {
            // Arrange
            string id = "id";
            string iotHub = "foo.azure-devices.net";
            var routerConfig = new RouterConfig(Enumerable.Empty<Route>());

            var messageStore = new Mock<IMessageStore>();
            messageStore.Setup(m => m.SetTimeToLive(It.IsAny<TimeSpan>()));

            var storageSpaceChecker = new Mock<IStorageSpaceChecker>();
            storageSpaceChecker.Setup(m => m.SetMaxSizeBytes(It.IsAny<Option<long>>()));

            TimeSpan updateFrequency = TimeSpan.FromMinutes(10);

            Endpoint GetEndpoint() => new ModuleEndpoint("id", Guid.NewGuid().ToString(), "in1", Mock.Of<IConnectionManager>(), Mock.Of<Core.IMessageConverter<IMessage>>());
            var endpointFactory = new Mock<IEndpointFactory>();
            endpointFactory.Setup(e => e.CreateSystemEndpoint($"$upstream")).Returns(GetEndpoint);
            var routeFactory = new EdgeRouteFactory(endpointFactory.Object);

            var endpointExecutorFactory = new Mock<IEndpointExecutorFactory>();
            endpointExecutorFactory.Setup(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()))
                .Returns<Endpoint, IList<uint>, ICheckpointerFactory>((endpoint, priorities, checkpointerFactory) => Task.FromResult(Mock.Of<IEndpointExecutor>(e => e.Endpoint == endpoint)));
            Router router = await Router.CreateAsync(id, iotHub, routerConfig, endpointExecutorFactory.Object);

            var routes1 = Routes.Take(2)
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration1 = new StoreAndForwardConfiguration(7200);
            var edgeHubConfig1 = new EdgeHubConfig("1.0", routes1, storeAndForwardConfiguration1, Option.None<BrokerConfig>());

            var routes2 = Routes.Take(3)
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration2 = new StoreAndForwardConfiguration(7200);
            var edgeHubConfig2 = new EdgeHubConfig("1.0", routes2, storeAndForwardConfiguration2, Option.None<BrokerConfig>());

            var configProvider = new Mock<IConfigSource>();
            configProvider.SetupSequence(c => c.GetConfig())
                .Returns(async () =>
                {
                    await Task.Delay(5000);
                    return Option.Some(edgeHubConfig2);
                });

            configProvider.Setup(c => c.GetCachedConfig())
                .Returns(() => Task.FromResult(Option.Some(edgeHubConfig1)));

            // Act
            var configUpdater = new ConfigUpdater(router, messageStore.Object, updateFrequency, storageSpaceChecker.Object);
            await configUpdater.Init(configProvider.Object);

            // Assert
            // First only has updated from prefeched config
            Assert.Equal(2, router.Routes.Count);

            // After 6 seconds updates from init received
            await Task.Delay(TimeSpan.FromSeconds(6));
            Assert.Equal(3, router.Routes.Count);
        }

        [Fact]
        public async Task TestInitialConfigUpdate_WaitForInit()
        {
            // Arrange
            string id = "id";
            string iotHub = "foo.azure-devices.net";
            var routerConfig = new RouterConfig(Enumerable.Empty<Route>());

            var messageStore = new Mock<IMessageStore>();
            messageStore.Setup(m => m.SetTimeToLive(It.IsAny<TimeSpan>()));

            var storageSpaceChecker = new Mock<IStorageSpaceChecker>();
            storageSpaceChecker.Setup(m => m.SetMaxSizeBytes(It.IsAny<Option<long>>()));

            TimeSpan updateFrequency = TimeSpan.FromSeconds(10);

            Endpoint GetEndpoint() => new ModuleEndpoint("id", Guid.NewGuid().ToString(), "in1", Mock.Of<IConnectionManager>(), Mock.Of<Core.IMessageConverter<IMessage>>());
            var endpointFactory = new Mock<IEndpointFactory>();
            endpointFactory.Setup(e => e.CreateSystemEndpoint($"$upstream")).Returns(GetEndpoint);
            var routeFactory = new EdgeRouteFactory(endpointFactory.Object);

            var endpointExecutorFactory = new Mock<IEndpointExecutorFactory>();
            endpointExecutorFactory.Setup(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()))
                .Returns<Endpoint, IList<uint>, ICheckpointerFactory>((endpoint, priorities, checkpointerFactory) => Task.FromResult(Mock.Of<IEndpointExecutor>(e => e.Endpoint == endpoint)));
            Router router = await Router.CreateAsync(id, iotHub, routerConfig, endpointExecutorFactory.Object);

            var routes1 = Routes.Take(2)
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration1 = new StoreAndForwardConfiguration(7200);
            var edgeHubConfig1 = new EdgeHubConfig("1.0", routes1, storeAndForwardConfiguration1, Option.None<BrokerConfig>());

            var configProvider = new Mock<IConfigSource>();
            configProvider.SetupSequence(c => c.GetConfig())
                .Returns(async () =>
                {
                    await Task.Delay(5000);
                    return Option.Some(edgeHubConfig1);
                });

            configProvider.Setup(c => c.GetCachedConfig())
                .Returns(() => Task.FromResult(Option.None<EdgeHubConfig>()));

            // Act
            var configUpdater = new ConfigUpdater(router, messageStore.Object, updateFrequency, storageSpaceChecker.Object);
            await configUpdater.Init(configProvider.Object);

            // Assert
            Assert.Equal(2, router.Routes.Count);

            // After 6 seconds no updates
            await Task.Delay(TimeSpan.FromSeconds(6));
            Assert.Equal(2, router.Routes.Count);
        }

        [Fact]
        public async Task TestPeriodicConfigUpdate()
        {
            // Arrange
            string id = "id";
            string iotHub = "foo.azure-devices.net";
            var routerConfig = new RouterConfig(Enumerable.Empty<Route>());

            var messageStore = new Mock<IMessageStore>();
            messageStore.Setup(m => m.SetTimeToLive(It.IsAny<TimeSpan>()));

            var storageSpaceChecker = new Mock<IStorageSpaceChecker>();
            storageSpaceChecker.Setup(m => m.SetMaxSizeBytes(It.IsAny<Option<long>>()));

            TimeSpan updateFrequency = TimeSpan.FromSeconds(10);

            Endpoint GetEndpoint() => new ModuleEndpoint("id", Guid.NewGuid().ToString(), "in1", Mock.Of<IConnectionManager>(), Mock.Of<Core.IMessageConverter<IMessage>>());
            var endpointFactory = new Mock<IEndpointFactory>();
            endpointFactory.Setup(e => e.CreateSystemEndpoint($"$upstream")).Returns(GetEndpoint);
            var routeFactory = new EdgeRouteFactory(endpointFactory.Object);

            var endpointExecutorFactory = new Mock<IEndpointExecutorFactory>();
            endpointExecutorFactory.Setup(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()))
                .Returns<Endpoint, IList<uint>, ICheckpointerFactory>((endpoint, priorities, checkpointerFactory) => Task.FromResult(Mock.Of<IEndpointExecutor>(e => e.Endpoint == endpoint)));
            Router router = await Router.CreateAsync(id, iotHub, routerConfig, endpointExecutorFactory.Object);

            var routes1 = Routes.Take(2)
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration1 = new StoreAndForwardConfiguration(7200);
            var edgeHubConfig1 = new EdgeHubConfig("1.0", routes1, storeAndForwardConfiguration1, Option.None<BrokerConfig>());

            var routes2 = Routes.Take(2)
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration2 = new StoreAndForwardConfiguration(7200);
            var edgeHubConfig2 = new EdgeHubConfig("1.0", routes2, storeAndForwardConfiguration2, Option.None<BrokerConfig>());

            var routes3 = Routes.Take(2)
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration3 = new StoreAndForwardConfiguration(7200);
            var edgeHubConfig3 = new EdgeHubConfig("1.0", routes3, storeAndForwardConfiguration3, Option.None<BrokerConfig>());

            var routes4 = Routes.Skip(2)
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration4 = new StoreAndForwardConfiguration(7200);
            var edgeHubConfig4 = new EdgeHubConfig("1.0", routes4, storeAndForwardConfiguration4, Option.None<BrokerConfig>());

            var routes5 = Routes
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration5 = new StoreAndForwardConfiguration(7200);
            var edgeHubConfig5 = new EdgeHubConfig("1.0", routes5, storeAndForwardConfiguration5, Option.None<BrokerConfig>());

            var routes6 = Routes
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration6 = new StoreAndForwardConfiguration(3600);
            var edgeHubConfig6 = new EdgeHubConfig("1.0", routes6, storeAndForwardConfiguration6, Option.None<BrokerConfig>());

            var routes7 = Routes
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration7 = new StoreAndForwardConfiguration(3600, new StoreLimits(10L));
            var edgeHubConfig7 = new EdgeHubConfig("1.0", routes7, storeAndForwardConfiguration7, Option.None<BrokerConfig>());

            var routes8 = Routes
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration8 = new StoreAndForwardConfiguration(3600, new StoreLimits(20L));
            var edgeHubConfig8 = new EdgeHubConfig("1.0", routes8, storeAndForwardConfiguration8, Option.None<BrokerConfig>());

            var configProvider = new Mock<IConfigSource>();
            configProvider.SetupSequence(c => c.GetConfig())
                .ReturnsAsync(Option.Some(edgeHubConfig1))
                .ReturnsAsync(Option.Some(edgeHubConfig2))
                .ReturnsAsync(Option.Some(edgeHubConfig3))
                .ReturnsAsync(Option.Some(edgeHubConfig4))
                .ReturnsAsync(Option.Some(edgeHubConfig5))
                .ReturnsAsync(Option.Some(edgeHubConfig6))
                .ReturnsAsync(Option.Some(edgeHubConfig7))
                .ReturnsAsync(Option.Some(edgeHubConfig8))
                .ReturnsAsync(Option.Some(edgeHubConfig8));
            configProvider.Setup(c => c.GetCachedConfig())
                .Returns(() => Task.FromResult(Option.None<EdgeHubConfig>()));

            // Act
            var configUpdater = new ConfigUpdater(router, messageStore.Object, updateFrequency, storageSpaceChecker.Object);
            await configUpdater.Init(configProvider.Object);

            // Assert
            configProvider.Verify(c => c.GetConfig(), Times.Once);
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Once);
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => x.Equals(Option.None<long>()))), Times.Once);

            // After 5 seconds, the periodic task should not have run.
            await Task.Delay(TimeSpan.FromSeconds(5));
            configProvider.Verify(c => c.GetConfig(), Times.Once);
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Once);
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => x.Equals(Option.None<long>()))), Times.Once);

            await Task.Delay(TimeSpan.FromSeconds(20));
            configProvider.Verify(c => c.GetConfig(), Times.Exactly(3));
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Exactly(3));
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => x.Equals(Option.None<long>()))), Times.Exactly(3));

            await Task.Delay(TimeSpan.FromSeconds(10));
            configProvider.Verify(c => c.GetConfig(), Times.Exactly(4));
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Exactly(4));
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => x.Equals(Option.None<long>()))), Times.Exactly(4));

            await Task.Delay(TimeSpan.FromSeconds(10));
            configProvider.Verify(c => c.GetConfig(), Times.Exactly(5));
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Exactly(5));
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => x.Equals(Option.None<long>()))), Times.Exactly(5));

            await Task.Delay(TimeSpan.FromSeconds(10));
            configProvider.Verify(c => c.GetConfig(), Times.Exactly(6));
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Exactly(6));
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => x.Equals(Option.None<long>()))), Times.Exactly(6));

            await Task.Delay(TimeSpan.FromSeconds(10));
            configProvider.Verify(c => c.GetConfig(), Times.Exactly(7));
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Exactly(7));
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => !x.Equals(Option.None<long>()))), Times.Exactly(1));

            await Task.Delay(TimeSpan.FromSeconds(10));
            configProvider.Verify(c => c.GetConfig(), Times.Exactly(8));
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Exactly(8));
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => !x.Equals(Option.None<long>()))), Times.Exactly(2));

            await Task.Delay(TimeSpan.FromSeconds(10));
            configProvider.Verify(c => c.GetConfig(), Times.Exactly(9));
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Exactly(8));
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => !x.Equals(Option.None<long>()))), Times.Exactly(2));
        }

        [Fact]
        public async Task TestPeriodicAndCallbackConfigUpdate()
        {
            // Arrange
            string id = "id";
            string iotHub = "foo.azure-devices.net";
            var routerConfig = new RouterConfig(Enumerable.Empty<Route>());

            var messageStore = new Mock<IMessageStore>();
            messageStore.Setup(m => m.SetTimeToLive(It.IsAny<TimeSpan>()));

            var storageSpaceChecker = new Mock<IStorageSpaceChecker>();
            storageSpaceChecker.Setup(m => m.SetMaxSizeBytes(It.IsAny<Option<long>>()));

            TimeSpan updateFrequency = TimeSpan.FromSeconds(10);

            Endpoint GetEndpoint() => new ModuleEndpoint("id", Guid.NewGuid().ToString(), "in1", Mock.Of<IConnectionManager>(), Mock.Of<Core.IMessageConverter<IMessage>>());
            var endpointFactory = new Mock<IEndpointFactory>();
            endpointFactory.Setup(e => e.CreateSystemEndpoint($"$upstream")).Returns(GetEndpoint);
            var routeFactory = new EdgeRouteFactory(endpointFactory.Object);

            var endpointExecutorFactory = new Mock<IEndpointExecutorFactory>();
            endpointExecutorFactory.Setup(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()))
                .Returns<Endpoint, IList<uint>, ICheckpointerFactory>((endpoint, priorities, checkpointerFactory) => Task.FromResult(Mock.Of<IEndpointExecutor>(e => e.Endpoint == endpoint)));
            Router router = await Router.CreateAsync(id, iotHub, routerConfig, endpointExecutorFactory.Object);

            var routes1 = Routes.Take(2)
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration1 = new StoreAndForwardConfiguration(7200);
            var edgeHubConfig1 = new EdgeHubConfig("1.0", routes1, storeAndForwardConfiguration1, Option.None<BrokerConfig>());

            var routes2 = Routes.Take(3)
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration2 = new StoreAndForwardConfiguration(7200);
            var edgeHubConfig2 = new EdgeHubConfig("1.0", routes2, storeAndForwardConfiguration2, Option.None<BrokerConfig>());

            var routes3 = Routes.Take(4)
                .ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            var storeAndForwardConfiguration3 = new StoreAndForwardConfiguration(7200);
            var edgeHubConfig3 = new EdgeHubConfig("1.0", routes3, storeAndForwardConfiguration3, Option.None<BrokerConfig>());

            var configProvider = new Mock<IConfigSource>();
            configProvider.SetupSequence(c => c.GetConfig())
                .ReturnsAsync(Option.Some(edgeHubConfig1))
                .ReturnsAsync(Option.Some(edgeHubConfig2))
                .ReturnsAsync(Option.Some(edgeHubConfig3));
            configProvider.Setup(c => c.GetCachedConfig())
                .Returns(() => Task.FromResult(Option.None<EdgeHubConfig>()));

            // Act
            var configUpdater = new ConfigUpdater(router, messageStore.Object, updateFrequency, storageSpaceChecker.Object);
            IConfigSource config = configProvider.Object;
            await configUpdater.Init(config);

            // Assert
            configProvider.Verify(c => c.GetConfig(), Times.Once);
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Once);
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => x.Equals(Option.None<long>()))), Times.Once);

            // call update with no changes
            configProvider.Raise(m => m.ConfigUpdated += null, config, edgeHubConfig1);
            configProvider.Verify(c => c.GetConfig(), Times.Exactly(1));
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Once);
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => x.Equals(Option.None<long>()))), Times.Once);

            await Task.Delay(TimeSpan.FromSeconds(12));
            configProvider.Verify(c => c.GetConfig(), Times.Exactly(2));
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Exactly(2));
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => x.Equals(Option.None<long>()))), Times.Exactly(2));

            // call update with changes
            configProvider.Raise(m => m.ConfigUpdated += null, config, edgeHubConfig3);
            configProvider.Verify(c => c.GetConfig(), Times.Exactly(2));
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Exactly(3));
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => x.Equals(Option.None<long>()))), Times.Exactly(3));

            await Task.Delay(TimeSpan.FromSeconds(10));
            configProvider.Verify(c => c.GetConfig(), Times.Exactly(3));
            endpointExecutorFactory.Verify(e => e.CreateAsync(It.IsAny<Endpoint>(), It.IsAny<IList<uint>>(), It.IsAny<ICheckpointerFactory>()), Times.Once);
            messageStore.Verify(m => m.SetTimeToLive(It.IsAny<TimeSpan>()), Times.Exactly(3));
            storageSpaceChecker.Verify(m => m.SetMaxSizeBytes(It.Is<Option<long>>(x => x.Equals(Option.None<long>()))), Times.Exactly(3));
        }
    }
}
