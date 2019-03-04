// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;

    public interface IConfigSource : IDisposable
    {
        IConfiguration Configuration { get; }

        Task<DeploymentConfigInfo> GetDeploymentConfigInfoAsync();
    }
}
