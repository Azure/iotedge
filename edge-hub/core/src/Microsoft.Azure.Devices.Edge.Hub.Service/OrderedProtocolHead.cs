// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class OrderedProtocolHead : IProtocolHead
    {
        readonly IList<IProtocolHead> underlyingProtocolHeads;

        public OrderedProtocolHead(IList<IProtocolHead> underlyingProtocolHeads)
        {
            this.underlyingProtocolHeads = Preconditions.CheckNotNull(underlyingProtocolHeads, nameof(underlyingProtocolHeads));
        }

        public string Name => string.Join(", ", this.underlyingProtocolHeads.Select(ph => ph.Name));

        public async Task StartAsync()
        {
            foreach (var protocolHead in this.underlyingProtocolHeads)
            {
                await protocolHead.StartAsync();
            }
        }

        public async Task CloseAsync(CancellationToken token)
        {
            foreach (var protocolHead in this.underlyingProtocolHeads.Reverse())
            {
                await protocolHead.CloseAsync(token);
            }
        }

        public void Dispose()
        {
            foreach (var protocolHead in this.underlyingProtocolHeads.Reverse())
            {
                protocolHead.Dispose();
            }
        }
    }
}
