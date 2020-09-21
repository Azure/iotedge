// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Nito.AsyncEx;
    using Xunit;

    [Unit]
    public class MqttBrokerNotifierTest
    {
        static readonly TimeSpan HalfSecond = TimeSpan.FromMilliseconds(500);

        [Fact]
        public Task AuthAgentProtocolHeadThenBrokerConnectThenIdentitiesCache() => this.MqttBrokerNotifierWithVariousSignalSequenceAsync(0, 1, 2);

        [Fact]
        public Task AuthAgentProtocolHeadThenIdentitiesCacheThenBrokerConnect() => this.MqttBrokerNotifierWithVariousSignalSequenceAsync(0, 2, 1);

        [Fact]
        public Task BrokerConnectThenAuthAgentProtocolHeadThenIdentitiesCache() => this.MqttBrokerNotifierWithVariousSignalSequenceAsync(1, 0, 2);

        [Fact]
        public Task BrokerConnectThenIdentitiesCacheThenAuthAgentProtocolHead() => this.MqttBrokerNotifierWithVariousSignalSequenceAsync(1, 2, 0);

        [Fact]
        public Task IdentitiesCacheThenAuthAgentProtocolHeadThenBrokerConnect() => this.MqttBrokerNotifierWithVariousSignalSequenceAsync(2, 0, 1);

        [Fact]
        public Task IdentitiesCacheThenBrokerConnectThenAuthAgentProtocolHead() => this.MqttBrokerNotifierWithVariousSignalSequenceAsync(2, 1, 0);

        async Task MqttBrokerNotifierWithVariousSignalSequenceAsync(int firstSignalIndex, int secondSignalIndex, int thirdSignalIndex)
        {
            (var authAgentProtocolHead, var mqttBrokerConnectorMock, var signals) = this.TestSetup();
            await Task.Delay(HalfSecond);
            mqttBrokerConnectorMock.Verify(m => m.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never());

            signals[firstSignalIndex].Set();
            await Task.Delay(HalfSecond);
            mqttBrokerConnectorMock.Verify(m => m.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never());

            signals[secondSignalIndex].Set();
            await Task.Delay(HalfSecond);
            mqttBrokerConnectorMock.Verify(m => m.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never());

            signals[thirdSignalIndex].Set();
            await Task.Delay(HalfSecond);
            mqttBrokerConnectorMock.Verify(m => m.SendAsync("$ehc/ready", It.Is<byte[]>(val => Encoding.UTF8.GetBytes("\"EdgeHub is ready to serve.\"").SequenceEqual(val))), Times.Once());
            await authAgentProtocolHead.CloseAsync(CancellationToken.None);
        }

        (AuthAgentProtocolHead, Mock<IMqttBrokerConnector>, AsyncManualResetEvent[]) TestSetup()
        {
            var authAgentProtocolHead = this.CreateAuthAgentProtocolHeadAcceptEverything();
            var mqttBrokerConnectorMock = new Mock<IMqttBrokerConnector>();
            var deviceScopeIdentitiesCacheMock = new Mock<IDeviceScopeIdentitiesCache>();
            var authAgentProtocolHeadStartSignal = new AsyncManualResetEvent(false);
            var mqttBrokerConnectorConnectSignal = new AsyncManualResetEvent(false);
            var deviceScopeIdentitiesCacheInitializeSignal = new AsyncManualResetEvent(false);
            var deviceScopeIdentitiesCacheSupplier = Task.FromResult(deviceScopeIdentitiesCacheMock.Object);
            var authAgentProtocolHeadSupplier = Task.FromResult(authAgentProtocolHead);
            var mqttBrokerNotifier = new MqttBrokerNotifier(mqttBrokerConnectorMock.Object, deviceScopeIdentitiesCacheSupplier, authAgentProtocolHeadSupplier);

            Task.Run(async () =>
            {
                await authAgentProtocolHeadStartSignal.WaitAsync();
                await authAgentProtocolHead.StartAsync();
            });

            Task.Run(async () =>
            {
                await mqttBrokerConnectorConnectSignal.WaitAsync();
                mqttBrokerConnectorMock.Raise(m => m.OnConnected += null, new EventArgs());
            });

            deviceScopeIdentitiesCacheMock.Setup(d => d.WaitForIntialialCachingCompleteAsync()).Returns(deviceScopeIdentitiesCacheInitializeSignal.WaitAsync);
            mqttBrokerConnectorMock.Setup(m => m.ConnectAsync(It.IsAny<string>(), It.IsAny<int>())).Returns(Task.CompletedTask);
            mqttBrokerConnectorMock.Setup(m => m.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.FromResult(true));
            return (authAgentProtocolHead, mqttBrokerConnectorMock, new AsyncManualResetEvent[] { authAgentProtocolHeadStartSignal, mqttBrokerConnectorConnectSignal, deviceScopeIdentitiesCacheInitializeSignal });
        }

        AuthAgentProtocolHead CreateAuthAgentProtocolHeadAcceptEverything()
        {
            var authenticator = Mock.Of<IAuthenticator>();
            var usernameParser = new MqttUsernameParser();
            var credFactory = new ClientCredentialsFactory(new IdentityProvider("testhub"));
            var sysIdProvider = Mock.Of<ISystemComponentIdProvider>();
            Mock.Get(authenticator).Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).Returns(Task.FromResult(true));
            Mock.Get(sysIdProvider).Setup(a => a.EdgeHubBridgeId).Returns("testdev/$edgeHub/$bridge");
            return new AuthAgentProtocolHead(authenticator, usernameParser, credFactory, sysIdProvider, new AuthAgentProtocolHeadConfig(7123, "/authenticate/"));
        }
    }
}
