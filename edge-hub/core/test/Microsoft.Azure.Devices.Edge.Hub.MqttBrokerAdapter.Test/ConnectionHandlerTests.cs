// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Text;
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

            var connectionProviderGetter = Task.FromResult(connectionProvider);
            var identityProvider = new IdentityProvider("hub");
            var systemComponentIdProvider = new SystemComponentIdProvider(
                                                    new TokenCredentials(
                                                            new ModuleIdentity("hub", "device1", "$edgeHub"), "token", "prodinfo", Option.Some("x"), Option.None<string>(), false));

            var identity = identityProvider.Create("device_test");

            var brokerConnector = Mock.Of<IMqttBrokerConnector>();
            Mock.Get(brokerConnector)
                .Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(() => Task.FromResult(true));

            var sut = default(ConnectionHandler);

            DeviceProxy.Factory deviceProxyFactory = GetProxy;

            sut = new ConnectionHandler(connectionProviderGetter, identityProvider, systemComponentIdProvider, deviceProxyFactory);
            sut.SetConnector(brokerConnector);
            await sut.HandleAsync(new MqttPublishInfo("$edgehub/connected", Encoding.UTF8.GetBytes("[\"device_test\"]")));

            await sut.CloseConnectionAsync(identity);

            Mock.Get(brokerConnector).Verify(
                c => c.SendAsync(
                            It.Is<string>(topic => topic.Equals("$edgehub/disconnect")),
                            It.Is<byte[]>(payload => Encoding.UTF8.GetString(payload).Equals($"\"device_test\""))),
                Times.Once);

            DeviceProxy GetProxy(IIdentity identity)
            {
                return new DeviceProxy(
                                identity,
                                sut,
                                Mock.Of<ITwinHandler>(),
                                Mock.Of<IModuleToModuleMessageHandler>(),
                                Mock.Of<ICloud2DeviceMessageHandler>(),
                                Mock.Of<IDirectMethodHandler>());
            }
        }
    }
}
