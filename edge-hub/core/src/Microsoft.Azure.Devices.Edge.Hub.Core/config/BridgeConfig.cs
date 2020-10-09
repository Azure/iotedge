// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    /// <summary>
    /// Domain object that represents Bridge configuration for MQTT Broker.
    ///
    /// This object is being eventually constructed from the EdgeHub twin's desired properties.
    /// See <see cref="EdgeHubDesiredProperties"/> for DTO.
    /// </summary>
    public class BridgeConfig : List<string>
    {
    }
}
