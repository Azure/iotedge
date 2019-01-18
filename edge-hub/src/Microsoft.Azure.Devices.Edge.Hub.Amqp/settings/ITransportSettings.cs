// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings
{
    using Microsoft.Azure.Amqp.Transport;

    public interface ITransportSettings
    {
        string HostName { get; }

        TransportSettings Settings { get; }
    }
}
