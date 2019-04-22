// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public interface ILogsProcessor
    {
        Task<IReadOnlyList<ModuleLogMessage>> GetMessages(string id, Stream stream, ModuleLogFilter filter);

        Task<IReadOnlyList<string>> GetText(string id, Stream stream, ModuleLogFilter filter);

        Task ProcessLogsStream(string id, Stream stream, ModuleLogOptions logOptions, Func<ArraySegment<byte>, Task> callback);
    }
}
