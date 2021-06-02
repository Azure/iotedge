// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Collections.Concurrent;
    using System.Text;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ConnectionHandlerTests
    {
        [Fact]
        public async Task CloseSendsDisconnectSignal()
        {
            var connectionProvider = Mock.Of<IConnectionProvider>();
            Mock.Get(connectionProvider)
                .Setup(p => p.GetDeviceListenerAsync(
                                It.IsAny<Identity>(),
                                It.IsAny<Option<string>>()))
                .Returns(Task.FromResult(Mock.Of<IDeviceListener>()));

            var authenticator = Mock.Of<IAuthenticator>();
            Mock.Get(authenticator)
                .Setup(p => p.AuthenticateAsync(It.IsAny<IClientCredentials>()))
                .Returns(Task.FromResult(true));

            var connectionProviderGetter = Task.FromResult(connectionProvider);
            var authenticatorGetter = Task.FromResult(authenticator);

            var identityProvider = new IdentityProvider("hub");
            var systemComponentIdProvider = new SystemComponentIdProvider(
                                                    new TokenCredentials(
                                                            new ModuleIdentity("hub", "device1", "$edgeHub"), "token", "prodinfo", Option.Some("x"), Option.None<string>(), false));

            var identity = identityProvider.Create("device_test");

            var brokerConnector = Mock.Of<IMqttBrokerConnector>();
            Mock.Get(brokerConnector)
                .Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<bool>()))
                .Returns(() => Task.FromResult(true));

            var sut = default(ConnectionHandler);

            DeviceProxy.Factory deviceProxyFactory = GetProxy;

            sut = new ConnectionHandler(connectionProviderGetter, authenticatorGetter, identityProvider, systemComponentIdProvider, deviceProxyFactory);
            sut.SetConnector(brokerConnector);
            await sut.HandleAsync(new MqttPublishInfo("$edgehub/connected", Encoding.UTF8.GetBytes("[\"device_test\"]")));

            await sut.CloseConnectionAsync(identity);

            Mock.Get(brokerConnector).Verify(
                c => c.SendAsync(
                            It.Is<string>(topic => topic.Equals("$edgehub/disconnect")),
                            It.Is<byte[]>(payload => Encoding.UTF8.GetString(payload).Equals($"\"device_test\"")),
                            false),
                Times.Once);

            DeviceProxy GetProxy(IIdentity identity, bool isDirectClient = true)
            {
                return new DeviceProxy(
                                identity,
                                isDirectClient,
                                sut,
                                Mock.Of<ITwinHandler>(),
                                Mock.Of<IModuleToModuleMessageHandler>(),
                                Mock.Of<ICloud2DeviceMessageHandler>(),
                                Mock.Of<IDirectMethodHandler>());
            }
        }

        [Fact]
        public async Task ReconnectAsDirectRemovesNestedInstance()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            Mock.Get(connectionManager)
                .Setup(p => p.RemoveDeviceConnection(It.IsAny<string>()))
                .Returns(() => Task.CompletedTask);

            var edgeHub = Mock.Of<IEdgeHub>();

            var connectionProvider = new ConnectionProvider(connectionManager, edgeHub, TimeSpan.FromSeconds(30)) as IConnectionProvider;

            var authenticator = Mock.Of<IAuthenticator>();
            Mock.Get(authenticator)
                .Setup(p => p.AuthenticateAsync(It.IsAny<IClientCredentials>()))
                .Returns(Task.FromResult(true));

            var connectionProviderGetter = Task.FromResult(connectionProvider);
            var authenticatorGetter = Task.FromResult(authenticator);

            var identityProvider = new IdentityProvider("hub");
            var systemComponentIdProvider = new SystemComponentIdProvider(
                                                    new TokenCredentials(
                                                            new ModuleIdentity("hub", "device1", "$edgeHub"), "token", "prodinfo", Option.Some("x"), Option.None<string>(), false));

            var brokerConnector = Mock.Of<IMqttBrokerConnector>();
            Mock.Get(brokerConnector)
                .Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<bool>()))
                .Returns(() => Task.FromResult(true));

            var sut = default(ConnectionHandler);

            DeviceProxy.Factory deviceProxyFactory = GetProxy;

            sut = new ConnectionHandler(connectionProviderGetter, authenticatorGetter, identityProvider, systemComponentIdProvider, deviceProxyFactory);
            sut.SetConnector(brokerConnector);

            await sut.GetOrCreateDeviceListenerAsync(new ModuleIdentity("hub", "device1", "direct_module"), true);
            await sut.GetOrCreateDeviceListenerAsync(new ModuleIdentity("hub", "device1", "indirect_module"), false);
            await sut.GetOrCreateDeviceListenerAsync(new ModuleIdentity("hub", "device1", "$edgeHub"), false);
            await sut.GetOrCreateDeviceListenerAsync(new ModuleIdentity("hub", "device1", "indirect_reconnecting"), false);

            // after indirect_reconnecting comes back as direct, the broker would send 3 connected clients: [direct_module, $edgeHub, indirect_reconnecting]
            // $edgeHub is indirect but listed, yet it should not be bothered as it is a special case.
            // indirect_module is not listed, but as indirect, it should not be removed
            // direct_module should keep its state
            // indirect_reconnecting should be recreated as direct

            await sut.HandleAsync(new MqttPublishInfo("$edgehub/connected", Encoding.UTF8.GetBytes("[\"device1/direct_module\", \"device1/$edgeHub\", \"device1/indirect_reconnecting\"]")));

            var knownConnections = sut.AsPrivateAccessible().knownConnections as ConcurrentDictionary<IIdentity, IDeviceListener>;

            Assert.Equal(4, knownConnections.Count());

            Assert.True(HasModule(knownConnections, "direct_module", true));
            Assert.True(HasModule(knownConnections, "indirect_module", false));
            Assert.True(HasModule(knownConnections, "$edgeHub", false));
            Assert.True(HasModule(knownConnections, "indirect_reconnecting", true));

            bool HasModule(ConcurrentDictionary<IIdentity, IDeviceListener> knownConnections, string name, bool isDirect)
            {
                return knownConnections.Values.OfType<IDeviceProxy>().Any(p => p.IsDirectClient == isDirect && p.Identity is ModuleIdentity i && string.Equals(i.ModuleId, name));
            }

            DeviceProxy GetProxy(IIdentity identity, bool isDirectClient = true)
            {
                return new DeviceProxy(
                                identity,
                                isDirectClient,
                                sut,
                                Mock.Of<ITwinHandler>(),
                                Mock.Of<IModuleToModuleMessageHandler>(),
                                Mock.Of<ICloud2DeviceMessageHandler>(),
                                Mock.Of<IDirectMethodHandler>());
            }
        }
    }
}
