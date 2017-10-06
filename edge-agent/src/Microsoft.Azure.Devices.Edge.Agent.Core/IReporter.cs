// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading.Tasks;

    public interface IReporter
    {
        Task ReportAsync(ModuleSet moduleSet);
    }
}
