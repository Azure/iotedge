// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings
{
    using Microsoft.Azure.Amqp.Transport;

    public interface ITransportSettings
    {
        string HostName { get; }

        TransportSettings Settings { get; }
    }
}
