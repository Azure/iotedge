// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILogsProvider
    {
        Task<byte[]> GetLogs(ModuleLogOptions logOptions, CancellationToken cancellationToken);

        Task GetLogsStream(ModuleLogOptions logOptions, Func<ArraySegment<byte>, Task> callback, CancellationToken cancellationToken);
    }
}
