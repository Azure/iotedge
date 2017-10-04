// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Threading.Tasks;

    public interface IConfigSource
    {
        Task<EdgeHubConfig> GetConfig();

        void SetConfigUpdatedCallback(Func<EdgeHubConfig, Task> callback);
    }
}
