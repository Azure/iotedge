// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
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
        readonly TimeSpan TimeoutInSecs;

        public EdgeHubProtocolHead(IList<IProtocolHead> underlyingProtocolHeads, ILogger logger, int timeoutInSecs)
        {
            this.underlyingProtocolHeads = Preconditions.CheckNotNull(underlyingProtocolHeads, nameof(underlyingProtocolHeads));
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.TimeoutInSecs = TimeSpan.FromSeconds(timeoutInSecs);
        }

        public string Name => $"({string.Join(", ", this.underlyingProtocolHeads.Select(ph => ph.Name))})";

        public Task StartAsync()
        {
            this.logger.LogInformation($"Starting protocol heads - {this.Name}");
            return Task.WhenAll(this.underlyingProtocolHeads.Select(protocolHead => protocolHead.StartAsync()));
        }

        public Task CloseAsync(CancellationToken token)
        {
            this.logger.LogInformation($"Closing protocol heads - {this.Name}");
            try
            {
                return Task.WhenAll(this.underlyingProtocolHeads.Select(protocolHead => protocolHead.CloseAsync(token))).TimeoutAfter(TimeoutInSecs);
            }
            catch (TimeoutException ex)
            {
                this.logger.LogError("Could not close all protocol heads gracefully", ex);
                return Task.CompletedTask;
            }
        }

        public void Dispose()
        {
            foreach (IProtocolHead protocolHead in this.underlyingProtocolHeads)
            {
                protocolHead.Dispose();
            }
        }
    }
}
