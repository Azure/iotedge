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
    using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
    using Xunit;

    public class FailedEndpoint : Endpoint
    {
        readonly ErrorDetectionStrategy detectionStrategy;

        public Exception Exception { get; }

        public override string Type => nameof(FailedEndpoint);

        public FailedEndpoint(string id)
            : this(id, new Exception())
        {
        }

        public FailedEndpoint(string id, Exception exception)
            : this(id, id, string.Empty, exception)
        {
        }

        public FailedEndpoint(string id, string name, string iotHubName, Exception exception)
            : this(id, name, iotHubName, exception, new ErrorDetectionStrategy(ex => !ex.Message.Contains("nontransient")))
        {
        }

        public FailedEndpoint(string id, string name, string iotHubName, Exception exception, ErrorDetectionStrategy detectionStrategy)
            : base(id, name, iotHubName)
        {
            this.Exception = exception;
            this.detectionStrategy = detectionStrategy;
        }

        public override IProcessor CreateProcessor() => new Processor(this);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
        }

        class Processor : IProcessor
        {
            readonly FailedEndpoint endpoint;

            public Endpoint Endpoint => this.endpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => this.endpoint.detectionStrategy;

            public Processor(FailedEndpoint endpoint)
            {
                this.endpoint = endpoint;
            }

            public Task<ISinkResult<IMessage>> ProcessAsync(IMessage message, CancellationToken token) =>
                this.ProcessAsync(new[] { message }, token);

            public Task<ISinkResult<IMessage>> ProcessAsync(ICollection<IMessage> messages, CancellationToken token)
            {
                ISinkResult<IMessage> result = new SinkResult<IMessage>(new IMessage[0], messages, new SendFailureDetails(FailureKind.InternalError, this.endpoint.Exception));
                return Task.FromResult(result);
            }

            public Task CloseAsync(CancellationToken token) => TaskEx.Done;
        }
    }

    [ExcludeFromCodeCoverage]
    public class FailedEndpointTest : RoutingUnitTestBase
    {
        [Fact, Unit]
        public async Task SmokeTest()
        {
            var cts = new CancellationTokenSource();
            var endpoint = new FailedEndpoint("id1", "name1", "hub1", new InvalidOperationException());
            IProcessor processor = endpoint.CreateProcessor();

            Assert.True(processor.ErrorDetectionStrategy.IsTransient(new Exception()));

            Assert.Equal(endpoint, processor.Endpoint);
            ISinkResult<IMessage> result = await processor.ProcessAsync(new IMessage[0], cts.Token);
            Assert.True(result.SendFailureDetails.HasValue);
            result.SendFailureDetails.ForEach(ex => Assert.IsType<InvalidOperationException>(ex.RawException));
            Assert.True(processor.CloseAsync(CancellationToken.None).IsCompleted);

            var endpoint2 = new FailedEndpoint("id2");
            IProcessor processor2 = endpoint2.CreateProcessor();
            ISinkResult<IMessage> result2 = await processor2.ProcessAsync(new IMessage[0], cts.Token);
            Assert.True(result2.SendFailureDetails.HasValue);
            result2.SendFailureDetails.ForEach(ex => Assert.IsType<Exception>(ex.RawException));
        }
    }
}
