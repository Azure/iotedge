// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util;

    public interface IConfigSource
    {
        Task<Option<EdgeHubConfig>> GetConfig();

        void SetConfigUpdatedCallback(Func<EdgeHubConfig, Task> callback);
    }
}
