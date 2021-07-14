// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Bson;
    using Newtonsoft.Json.Serialization;
    using Xunit;

    public class BrokeredCloudProxyDispatcherTest
    {
        [Fact]
        public async Task SuccessfulMethodCallReturnsMethodResult()
        {
            var resultTopic = await this.DirectMethodCall(new DirectMethodResponse("123", new byte[] { 1, 2, 3 }, 201));
            Assert.Contains("/res/201/", resultTopic);
        }

        [Fact]
        public async Task FailedMethodCallReturnsHttpStatus()
        {
            var resultTopic = await this.DirectMethodCall(new DirectMethodResponse(new Exception("some error"), System.Net.HttpStatusCode.NotFound));
            Assert.Contains("/res/404/", resultTopic);
        }

        public async Task<string> DirectMethodCall(DirectMethodResponse response)
        {
            var connector = new Mock<IMqttBrokerConnector>();
            var edgeHub = new Mock<IEdgeHub>();

            var lastPayload = default(byte[]);

            var milestone = new SemaphoreSlim(0, 1);

            connector.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<bool>()))
                     .Callback<string, byte[], bool>(
                        (_, p, __) =>
                        {
                            lastPayload = p;
                            milestone.Release();
                        })
                     .Returns(() => Task.FromResult(true));

            edgeHub.Setup(e => e.InvokeMethodAsync(It.IsAny<string>(), It.IsAny<DirectMethodRequest>()))
                   .Returns(() => Task.FromResult(response));

            var sut = new BrokeredCloudProxyDispatcher();

            sut.BindEdgeHub(edgeHub.Object);
            sut.SetConnector(connector.Object);

            await sut.HandleAsync(new MqttPublishInfo("$downstream/dev_a/mod_1/methods/post/test/?$rid=123", Encoding.UTF8.GetBytes("{ \"test\":\"data\"}")));

            await milestone.WaitAsync();

            var packet = GetRpcPacket(lastPayload);

            return packet.Topic;
        }

        RpcPacket GetRpcPacket(byte[] payload)
        {
            var packet = default(RpcPacket);
            using (var reader = new BsonDataReader(new MemoryStream(payload)))
            {
                var serializer = new JsonSerializer
                {
                    ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new CamelCaseNamingStrategy()
                    }
                };

                packet = serializer.Deserialize<RpcPacket>(reader);
            }
            return packet;
        }
    }
}
