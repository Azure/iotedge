// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Reporters
{
    using System.Threading.Tasks;

    public class NullReporter : IReporter
    {
        public static NullReporter Instance { get; } = new NullReporter();

        public Task ReportAsync(ModuleSet moduleSet) => Task.CompletedTask;
    }
}
