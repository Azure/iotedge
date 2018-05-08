// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public interface IDeviceClient : IClient
    {
        Task RejectAsync(string messageId);

        Task<Message> ReceiveAsync(TimeSpan receiveMessageTimeout);
    }
}
