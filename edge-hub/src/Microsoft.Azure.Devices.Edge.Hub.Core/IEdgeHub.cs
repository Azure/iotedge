// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public interface IEdgeHub
    {
        Task ProcessDeviceMessage(IIdentity identity, IMessage message);

        Task ProcessDeviceMessageBatch(IIdentity identity, IEnumerable<IMessage> message);
    }
}