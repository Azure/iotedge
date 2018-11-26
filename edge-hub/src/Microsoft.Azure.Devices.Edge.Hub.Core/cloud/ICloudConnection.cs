// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util;

    public interface ICloudConnection
    {
        Option<ICloudProxy> CloudProxy { get; }

        bool IsActive { get; }

        Task<bool> CloseAsync();
    }
}
