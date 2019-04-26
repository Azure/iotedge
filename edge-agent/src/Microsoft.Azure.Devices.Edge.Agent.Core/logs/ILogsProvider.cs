// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILogsProvider
    {
        Task<byte[]> GetLogs(string id, ModuleLogOptions logOptions, CancellationToken cancellationToken);

        Task GetLogsStream(string id, ModuleLogOptions logOptions, Func<ArraySegment<byte>, Task> callback, CancellationToken cancellationToken);

        Task GetLogsStream(IList<(string id, ModuleLogOptions logOptions)> ids, Func<ArraySegment<byte>, Task> callback, CancellationToken cancellationToken);
    }
}
