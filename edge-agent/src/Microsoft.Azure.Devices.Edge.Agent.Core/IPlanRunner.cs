// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPlanRunner
    {
        Task<bool> ExecuteAsync(long deploymentId, Plan plan, CancellationToken token);
    }
}
