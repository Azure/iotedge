// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    interface IReportMetadata
    {
        string ExpectedSource { get; }

        string ActualSource { get; }

        ReportType ReportType { get; }

        TestOperationResultType TestOperationResultType { get; }
    }
}
