// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Xunit;

    class StalledEndpoint : Endpoint
    {
        public StalledEndpoint(string id)
            : this(id, id, string.Empty)
        {
        }

        public StalledEndpoint(string id, string name, string iotHubName)
            : base(id, name, iotHubName)
        {
        }

        public override string Type => nameof(StalledEndpoint);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
        }

        public override IProcessor CreateProcessor() => new Processor(this);

        class Processor : IProcessor
        {
            readonly StalledEndpoint endpoint;

            public Endpoint Endpoint => this.endpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(_ => true);

            public Processor(StalledEndpoint endpoint)
            {
                this.endpoint = endpoint;
            }

            public Task<ISinkResult<IMessage>> ProcessAsync(IMessage message, CancellationToken token) =>
                this.ProcessAsync(new[] { message }, token);

            public Task<ISinkResult<IMessage>> ProcessAsync(ICollection<IMessage> message, CancellationToken token)
            {
                var cts = new TaskCompletionSource<ISinkResult<IMessage>>();
                token.Register(() => cts.SetCanceled());
                return cts.Task;
            }

            public Task CloseAsync(CancellationToken token) => TaskEx.Done;
        }
    }

    public class StalledEndpointTest : RoutingUnitTestBase
    {
        [Fact, Unit]
        public void SmokeTest()
        {
            var endpoint = new StalledEndpoint("id1");
            IProcessor processor = endpoint.CreateProcessor();

            Assert.Equal(endpoint, processor.Endpoint);
            Assert.True(processor.ErrorDetectionStrategy.IsTransient(new Exception()));
            Assert.Equal(string.Empty, endpoint.IotHubName);

            var cts = new CancellationTokenSource();
            Task<ISinkResult<IMessage>> result = processor.ProcessAsync(new IMessage[] { }, cts.Token);
            Assert.False(result.IsCompleted);
            Assert.False(result.IsCanceled);
            Assert.False(result.IsFaulted);

            cts.Cancel();
            Assert.True(result.IsCompleted);
            Assert.True(result.IsCanceled);
            Assert.False(result.IsFaulted);
        }
    }
}