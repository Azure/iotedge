// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;
    using Xunit;

    class RevivableEndpoint : Endpoint
    {
        public IList<IMessage> Processed { get; }

        public Exception Exception { get; }

        public bool Failing { get; set; }

        public RevivableEndpoint(string id)
            : this(id, new Exception())
        {
        }

        public RevivableEndpoint(string id, Exception exception)
            : this(id, id, string.Empty, exception)
        {
        }

        public RevivableEndpoint(string id, string name, string iotHubName, Exception exception)
            : base(id, name, iotHubName)
        {
            this.Exception = exception;
            this.Processed = new List<IMessage>();
            this.Failing = false;
        }

        public override string Type => nameof(RevivableEndpoint);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
        }

        public override IProcessor CreateProcessor() => new Processor(this);

        class Processor : IProcessor
        {
            readonly RevivableEndpoint endpoint;

            public Endpoint Endpoint => this.endpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(_ => true );

            public Processor(RevivableEndpoint endpoint)
            {
                this.endpoint = endpoint;
            }

            public Task<ISinkResult<IMessage>> ProcessAsync(IMessage message, CancellationToken token) =>
                this.ProcessAsync(new[] { message }, token);

            public Task<ISinkResult<IMessage>> ProcessAsync(ICollection<IMessage> messages, CancellationToken token)
            {
                ISinkResult<IMessage> result;
                if (this.endpoint.Failing)
                {
                    result = new SinkResult<IMessage>(new IMessage[0], messages, new SendFailureDetails(FailureKind.InternalError, this.endpoint.Exception));
                }
                else
                {
                    foreach (IMessage message in messages)
                    {
                        this.endpoint.Processed.Add(message);
                    }

                    result = new SinkResult<IMessage>(messages);
                }
                return Task.FromResult(result);
            }

            public Task CloseAsync(CancellationToken token) => TaskEx.Done;
        }
    }

    [ExcludeFromCodeCoverage]
    public class RevivableEndpointTest : RoutingUnitTestBase
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] {1, 2, 3}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} });
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] {2, 3, 1}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} });
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] {3, 1, 2}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} });

        [Fact, Unit]
        public async Task SmokeTest()
        {
            var endpoint = new RevivableEndpoint("id1");
            IProcessor processor = endpoint.CreateProcessor();

            Assert.True(processor.ErrorDetectionStrategy.IsTransient(new Exception()));

            Assert.Equal(endpoint, processor.Endpoint);
            Assert.Equal(new List<IMessage>(), endpoint.Processed);
            Assert.Equal(string.Empty, endpoint.IotHubName);

            ISinkResult<IMessage> result = await processor.ProcessAsync(new IMessage[0], CancellationToken.None);
            Assert.Equal(new IMessage[0], result.Succeeded);
            Assert.Equal(new List<IMessage>(), endpoint.Processed);

            var messages =  new[] { Message1, Message2, Message3 };
            ISinkResult<IMessage> result2 = await processor.ProcessAsync(messages, CancellationToken.None);
            Assert.Equal(new[] { Message1, Message2, Message3 }, result2.Succeeded);
            Assert.Equal(new List<IMessage> { Message1, Message2, Message3 }, endpoint.Processed);

            // set to failing
            endpoint.Failing = true;
            ISinkResult<IMessage> result3 = await processor.ProcessAsync(messages, CancellationToken.None);
            Assert.True(result3.SendFailureDetails.HasValue);
            Assert.Equal(new List<IMessage> { Message1, Message2, Message3 }, endpoint.Processed);

            // revive
            endpoint.Failing = false;
            ISinkResult<IMessage> result4 = await processor.ProcessAsync(messages, CancellationToken.None);
            Assert.Equal(new[] { Message1, Message2, Message3 }, result4.Succeeded);
            Assert.Equal(new List<IMessage> { Message1, Message2, Message3, Message1, Message2, Message3 }, endpoint.Processed);

            await processor.CloseAsync(CancellationToken.None);
        }
    }
}