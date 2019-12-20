// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    interface IReportMetadata
    {
        string ExpectedSource { get; }

        string ActualSource { get; }

        TestReportType TestReportType { get; }

        TestOperationResultType TestOperationResultType { get; }
    }
}
