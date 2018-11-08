// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
