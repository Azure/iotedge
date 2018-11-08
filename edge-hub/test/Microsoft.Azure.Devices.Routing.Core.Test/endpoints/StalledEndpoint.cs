// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Util;

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

        public override IProcessor CreateProcessor() => new Processor(this);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
        }

        class Processor : IProcessor
        {
            readonly StalledEndpoint endpoint;

            public Processor(StalledEndpoint endpoint)
            {
                this.endpoint = endpoint;
            }

            public Endpoint Endpoint => this.endpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(_ => true);

            public Task CloseAsync(CancellationToken token) => TaskEx.Done;

            public Task<ISinkResult<IMessage>> ProcessAsync(IMessage message, CancellationToken token) =>
                this.ProcessAsync(new[] { message }, token);

            public Task<ISinkResult<IMessage>> ProcessAsync(ICollection<IMessage> message, CancellationToken token)
            {
                var cts = new TaskCompletionSource<ISinkResult<IMessage>>();
                token.Register(() => cts.SetCanceled());
                return cts.Task;
            }
        }
    }
}
