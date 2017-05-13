// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;

    public class NullEndpoint : Endpoint
    {
        public NullEndpoint(string id)
            : base(id)
        {
        }

        public NullEndpoint(string id, string name, string iotHubName)
            : base(id, name, iotHubName)
        {
        }

        public override string Type => nameof(NullEndpoint);

        public override IProcessor CreateProcessor() => new Processor(this);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
        }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "NullEndpoint({0})", this.Id);

        class Processor : IProcessor
        {
            readonly NullEndpoint endpoint;

            public Endpoint Endpoint => this.endpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(_ => true);

            public Processor(NullEndpoint endpoint)
            {
                this.endpoint = Preconditions.CheckNotNull(endpoint);
            }

            public Task<ISinkResult<IMessage>> ProcessAsync(IMessage message, CancellationToken token) =>
                this.ProcessAsync(new[] { message }, token);

            public Task<ISinkResult<IMessage>> ProcessAsync(ICollection<IMessage> messages, CancellationToken token)
            {
                ISinkResult<IMessage> result = new SinkResult<IMessage>(messages);
                return Task.FromResult(result);
            }

            public Task CloseAsync(CancellationToken token) => TaskEx.Done;
        }
    }
}
