// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    class RevivableEndpoint : Endpoint
    {
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

        public Exception Exception { get; }

        public bool Failing { get; set; }

        public IList<IMessage> Processed { get; }

        public override string Type => nameof(RevivableEndpoint);

        public override IProcessor CreateProcessor() => new Processor(this);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
        }

        class Processor : IProcessor
        {
            readonly RevivableEndpoint endpoint;

            public Processor(RevivableEndpoint endpoint)
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
        }
    }
}
