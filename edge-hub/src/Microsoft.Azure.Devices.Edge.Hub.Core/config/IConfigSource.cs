// Copyright (c) Microsoft. All rights reserved.
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
