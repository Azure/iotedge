// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Represents a config source for EdgeHubConfig.
    /// Used to abstract in-memory config source vs twin config source.
    /// </summary>
    public interface IConfigSource
    {
        Task<Option<EdgeHubConfig>> GetCachedConfig();

        Task<Option<EdgeHubConfig>> GetConfig();

        /// <summary>
        /// Occurres when config is updated in the config source.
        /// For example, by a twin update pushed from the cloud.
        /// </summary>
        event EventHandler<EdgeHubConfig> ConfigUpdated;
    }
}
