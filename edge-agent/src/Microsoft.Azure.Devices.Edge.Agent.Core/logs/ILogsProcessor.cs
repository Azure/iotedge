// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public interface ILogsProcessor
    {
        Task<IReadOnlyList<ModuleLogMessage>> GetMessages(Stream stream, string moduleId, ModuleLogFilter filter);

        Task<IReadOnlyList<string>> GetText(Stream stream, string moduleId, ModuleLogFilter filter);

        Task ProcessLogsStream(Stream stream, ModuleLogOptions logOptions, Func<ArraySegment<byte>, Task> callback);
    }
}
