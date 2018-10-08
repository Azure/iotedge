// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface IDeviceIdentity : IIdentity
    {
        string DeviceId { get; }
    }
}
