// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    using Xunit;

    public class TestEndpoint : Endpoint
    {
        public TestEndpoint(string id)
            : this(id, id, string.Empty)
        {
        }

        public TestEndpoint(string id, string name, string iotHubName)
            : base(id, name, iotHubName)
        {
            this.Processed = new List<IMessage>();
        }

        public int N => this.Processed.Count;

        public IList<IMessage> Processed { get; }

        public override string Type => nameof(TestEndpoint);

        public override IProcessor CreateProcessor() => new Processor(this);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
        }

        public override string ToString() => $"TestEndpoint({this.Id})";

        class Processor : IProcessor
        {
            readonly TestEndpoint endpoint;

            public Processor(TestEndpoint endpoint)
            {
                this.endpoint = endpoint;
            }

            public Endpoint Endpoint => this.endpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(_ => true);

            public Task CloseAsync(CancellationToken token) => TaskEx.Done;

            public Task<ISinkResult<IMessage>> ProcessAsync(IMessage message, CancellationToken token) =>
                this.ProcessAsync(new[] { message }, token);

            public Task<ISinkResult<IMessage>> ProcessAsync(ICollection<IMessage> messages, CancellationToken token)
            {
                foreach (IMessage message in messages)
                {
                    this.endpoint.Processed.Add(message);
                }

                ISinkResult<IMessage> result = new SinkResult<IMessage>(messages);
                return Task.FromResult(result);
            }
        }
    }

    public class TestEndpointTest : RoutingUnitTestBase
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 1 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 3, 1, 2 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });

        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            var endpoint = new TestEndpoint("id1", "name1", "hub1");
            IProcessor processor = endpoint.CreateProcessor();

            Assert.Equal(endpoint, processor.Endpoint);
            Assert.True(processor.ErrorDetectionStrategy.IsTransient(new Exception()));
            Assert.Equal(new List<IMessage>(), endpoint.Processed);
            Assert.Equal("name1", endpoint.Name);
            Assert.Equal("hub1", endpoint.IotHubName);

            ISinkResult<IMessage> result = await processor.ProcessAsync(new IMessage[0], CancellationToken.None);
            Assert.Equal(new IMessage[0], result.Succeeded);
            Assert.Equal(new List<IMessage>(), endpoint.Processed);

            IMessage[] messages = new[] { Message1, Message2, Message3 };
            ISinkResult<IMessage> result2 = await processor.ProcessAsync(messages, CancellationToken.None);
            Assert.Equal(new[] { Message1, Message2, Message3 }, result2.Succeeded);
            Assert.Equal(new List<IMessage> { Message1, Message2, Message3 }, endpoint.Processed);
        }
    }
}
