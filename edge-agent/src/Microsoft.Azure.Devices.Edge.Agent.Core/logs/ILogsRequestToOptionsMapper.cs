// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILogsRequestToOptionsMapper
    {
        Task<IList<(string id, ModuleLogOptions logOptions)>> MapToLogOptions(IEnumerable<LogRequestItem> requestItems, CancellationToken cancellationToken);
    }
}
