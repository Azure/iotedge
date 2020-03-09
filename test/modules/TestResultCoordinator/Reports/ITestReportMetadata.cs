// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    public interface ITestReportMetadata
    {
        string[] ResultSources { get; }

        TestReportType TestReportType { get; }

        TestOperationResultType TestOperationResultType { get; }
    }
}
