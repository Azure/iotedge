// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;

    public class ConsoleEndpoint : Endpoint
    {
        readonly ConsoleColor color;

        public ConsoleEndpoint(string id)
            : this(id, id, ConsoleColor.Gray)
        {
        }

        public ConsoleEndpoint(string id, string name, ConsoleColor color)
            : this(id, name, string.Empty, color)
        {
        }

        public ConsoleEndpoint(string id, string name, string iotHubName, ConsoleColor color)
            : base(id, name, iotHubName)
        {
            this.color = Preconditions.CheckNotNull(color);
        }

        public override string Type => nameof(ConsoleEndpoint);

        public override IProcessor CreateProcessor() => new Processor(this);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
        }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "ConsoleEndpoint({0})", this.Id);

        class Processor : IProcessor
        {
            readonly ConsoleEndpoint endpoint;
            int count;

            public Endpoint Endpoint => this.endpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(_ => true);

            public Processor(ConsoleEndpoint endpoint)
            {
                this.endpoint = Preconditions.CheckNotNull(endpoint);
                this.count = 0;
            }

            public Task<ISinkResult<IMessage>> ProcessAsync(IMessage message, CancellationToken token) =>
                this.ProcessAsync(new[] { message }, token);

            public Task<ISinkResult<IMessage>> ProcessAsync(ICollection<IMessage> messages, CancellationToken token)
            {
                Console.ForegroundColor = this.endpoint.color;
                foreach (IMessage message in messages)
                {
                    if (this.count % 10000 == 0)
                    {
                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "({0}) {1}: {2}", this.count, this.endpoint, message));
                    }
                    this.count++;
                }
                Console.ResetColor();
                ISinkResult<IMessage> result = new SinkResult<IMessage>(messages);
                return Task.FromResult(result);
            }

            public Task CloseAsync(CancellationToken token) => TaskEx.Done;
        }
    }
}