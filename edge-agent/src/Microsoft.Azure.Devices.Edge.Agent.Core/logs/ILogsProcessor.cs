// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public interface ILogsProcessor
    {
        Task<IEnumerable<ModuleLogMessage>> GetMessages(Stream stream, string moduleId);

        Task<IEnumerable<string>> GetText(Stream stream);
    }
}
