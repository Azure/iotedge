// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System.Collections.Generic;

    /// <summary>
    /// Domain object that represents Bridge configuration for MQTT Broker.
    ///
    /// This object is being constructed from the EdgeHub twin's desired properties.
    /// See <see cref="EdgeHubDesiredProperties"/> for DTO.
    /// </summary>
    public class BridgeConfig : List<string>
    {
    }
}
