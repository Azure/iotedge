// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class EdgeHubProtocolHead : IProtocolHead
    {
        readonly IList<IProtocolHead> underlyingProtocolHeads;
        readonly ILogger logger;

        public EdgeHubProtocolHead(IList<IProtocolHead> underlyingProtocolHeads, ILogger logger)
        {
            this.underlyingProtocolHeads = Preconditions.CheckNotNull(underlyingProtocolHeads, nameof(underlyingProtocolHeads));
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        public string Name => $"({string.Join(", ", this.underlyingProtocolHeads.Select(ph => ph.Name))})";

        public Task CloseAsync(CancellationToken token)
        {
            this.logger.LogInformation($"Closing protocol heads - {this.Name}");
            return Task.WhenAll(this.underlyingProtocolHeads.Select(protocolHead => protocolHead.CloseAsync(token)));
        }

        public void Dispose()
        {
            foreach (IProtocolHead protocolHead in this.underlyingProtocolHeads)
            {
                protocolHead.Dispose();
            }
        }

        public Task StartAsync()
        {
            this.logger.LogInformation($"Starting protocol heads - {this.Name}");
            return Task.WhenAll(this.underlyingProtocolHeads.Select(protocolHead => protocolHead.StartAsync()));
        }
    }
}
