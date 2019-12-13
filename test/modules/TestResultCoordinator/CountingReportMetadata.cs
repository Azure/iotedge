// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    class CountingReportMetadata : IReportMetadata
    {
        public string ExpectedSource { get; set; }

        public string ActualSource { get; set; }

        public TestOperationResultType TestOperationResultType { get; set; }

        public ReportType ReportType { get; set; }
    }
}
