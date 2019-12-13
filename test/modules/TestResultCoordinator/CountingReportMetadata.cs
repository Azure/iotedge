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

        public override string ToString()
        {
            return $"ExpectedSource: {this.ExpectedSource}, ActualSource: {this.ActualSource}, TestOperationResultType: {this.TestOperationResultType.ToString()}, ReportType: {this.ReportType.ToString()}";
        }
    }
}
