// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    using Xunit;

    class PartialFailureEndpoint : Endpoint
    {
        public PartialFailureEndpoint(string id)
            : this(id, new Exception())
        {
        }

        public PartialFailureEndpoint(string id, Exception exception)
            : this(id, id, string.Empty, exception)
        {
        }

        public PartialFailureEndpoint(string id, string name, string iotHubName, Exception exception)
            : base(id, name, iotHubName)
        {
            this.Exception = exception;
        }

        public Exception Exception { get; }

        public override string Type => nameof(PartialFailureEndpoint);

        public override IProcessor CreateProcessor() =>
            new PartialFailureProcessor(this);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
        }

        class PartialFailureProcessor : IProcessor
        {
            readonly PartialFailureEndpoint endpoint;

            public PartialFailureProcessor(PartialFailureEndpoint endpoint)
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
                ISinkResult<IMessage> result;
                if (messages.Count <= 1)
                {
                    // Only fail if we have more than one message
                    result = new SinkResult<IMessage>(messages);
                }
                else
                {
                    IMessage[] successful = messages.Take(messages.Count / 2).ToArray();
                    IMessage[] failed = messages.Skip(messages.Count / 2).ToArray();
                    result = new SinkResult<IMessage>(successful, failed, new SendFailureDetails(FailureKind.InternalError, this.endpoint.Exception));
                }

                return Task.FromResult(result);
            }
        }
    }

    [ExcludeFromCodeCoverage]
    class PartialFailureEndpointTest : RoutingUnitTestBase
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3, 4 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 4, 1 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 3, 4, 1, 2 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage Message4 = new Message(TelemetryMessageSource.Instance, new byte[] { 4, 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });

        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            var endpoint = new PartialFailureEndpoint("id1");
            IProcessor processor = endpoint.CreateProcessor();

            Assert.True(processor.ErrorDetectionStrategy.IsTransient(new Exception()));

            Assert.Equal(endpoint, processor.Endpoint);
            ISinkResult<IMessage> result = await processor.ProcessAsync(new IMessage[] { }, CancellationToken.None);
            Assert.Equal(new IMessage[0], result.Succeeded);

            IMessage[] messages = new[] { Message1, Message2, Message3, Message4 };
            ISinkResult<IMessage> result2 = await processor.ProcessAsync(messages, CancellationToken.None);
            Assert.Equal(new[] { Message1, Message2 }, result2.Succeeded);
            Assert.Equal(new[] { Message3, Message4 }, result2.Failed);
        }
    }
}
