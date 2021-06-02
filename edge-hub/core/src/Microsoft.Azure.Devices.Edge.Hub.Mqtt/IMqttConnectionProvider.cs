// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    public interface IMqttConnectionProvider : IDisposable
    {
        Task<IMessagingBridge> Connect(IDeviceIdentity deviceidentity);
    }
}
