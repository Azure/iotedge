// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISuspendManager
    {
        Task<IDisposable> BeginUpdateCycleAsync(CancellationToken token);

        bool IsSuspended();

        Task SuspendUpdatesAsync(CancellationToken token);

        Task ResumeUpdatesAsync(CancellationToken token);
    }
}
