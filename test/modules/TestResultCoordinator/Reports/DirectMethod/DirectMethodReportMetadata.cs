// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using TestResultCoordinator.Reports;

    class DirectMethodReportMetadata : IReportMetadata
    {
        public DirectMethodReportMetadata(string expectedSource, string actualSource, TestReportType testReportType, TimeSpan tolerancePeriod)
        {
            this.ExpectedSource = expectedSource;
            this.ActualSource = actualSource;
            this.TestOperationResultType = TestOperationResultType.DirectMethod;
            this.TestReportType = testReportType;
            this.TolerancePeriod = tolerancePeriod;
        }

        public TimeSpan TolerancePeriod { get; }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public TestReportType TestReportType { get; }

        public TestOperationResultType TestOperationResultType { get; }
    }
}
