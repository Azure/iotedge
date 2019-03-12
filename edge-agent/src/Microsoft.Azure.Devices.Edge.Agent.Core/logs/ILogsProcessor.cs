// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public interface ILogsProcessor
    {
        Task<IReadOnlyList<ModuleLogMessage>> GetMessages(Stream stream, string moduleId);

        Task<IReadOnlyList<string>> GetText(Stream stream);
    }
}
