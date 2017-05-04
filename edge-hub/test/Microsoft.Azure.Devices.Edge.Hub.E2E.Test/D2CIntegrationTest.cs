// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Test;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class D2CIntegrationTest
    {
        readonly ILogger logger;

        public D2CIntegrationTest()
        {
            ILoggerFactory factory = new LoggerFactory()
                .AddConsole();
            this.logger = factory.CreateLogger<D2CIntegrationTest>();
        }

        public static IEnumerable<object[]> GetTestMessage()
        {
            IList<IMessage> messages = MessageHelper.GenerateMessages(1);
            return new List<object[]> { new object[] { messages[0] } };
        }

        [Theory]
        [Bvt]
        [MemberData(nameof(GetTestMessage))]
        public void SendMessageTest(IMessage message)
        {
            //DateTime startTime = DateTime.UtcNow;
            //Connection connection = null;
            //var mockConnectionManager = new Mock<IConnectionManager>();
            //mockConnectionManager.Setup(c => c.AddConnection(It.IsAny<string>(), It.IsAny<IDeviceProxy>(), It.IsAny<ICloudProxy>()))
            //    .Callback<string, IDeviceProxy, ICloudProxy>(((deviceId, deviceProxy, cloudProxy) => connection = new Connection(cloudProxy, deviceProxy)));
            //mockConnectionManager.Setup(c => c.GetConnection(It.IsAny<string>())).Returns(() => connection);

            //var dispatcher = new Dispatcher(mockConnectionManager.Object);
            //var router = new Router(dispatcher);

            //var mockAuthenticator = new Mock<IAuthenticator>();
            //mockAuthenticator.Setup(a => a.Authenticate(It.IsAny<string>())).Returns(Task.FromResult(true));

            //var cloudProxyProvider = new CloudProxyProvider(this.logger, TransportHelper.AmqpTcpTransportSettings, new MessageConverter());
            //var mockDeviceProxy = new Mock<IDeviceProxy>();

            //IConnectionProvider connectionProvider = new ConnectionProvider(
            //    mockConnectionManager.Object,
            //    router,
            //    dispatcher,
            //    mockAuthenticator.Object,
            //    cloudProxyProvider);

            //string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");

            //Try<IDeviceListener> deviceListener = await connectionProvider.GetDeviceListener(deviceConnectionString, mockDeviceProxy.Object);
            //Assert.True(deviceListener.Success);

            //await deviceListener.Value.ReceiveMessage(message);

            //string eventHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("eventHubConnStrKey");
            //var eventHubReceiver = new EventHubReceiver(eventHubConnectionString);
            //IList<EventData> cloudMessages = await eventHubReceiver.GetMessagesFromAllPartitions(startTime);
            //Assert.NotNull(cloudMessages);
            //Assert.NotEmpty(cloudMessages);
            //Assert.True(MessageHelper.CompareMessagesAndEventData(new List<IMessage> { message }, cloudMessages));
            //await eventHubReceiver.Close();
        }
    }
}