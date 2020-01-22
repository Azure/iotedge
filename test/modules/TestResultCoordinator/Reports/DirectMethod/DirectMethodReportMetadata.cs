// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Reports;

    class DirectMethodReportMetadata : ITestReportMetadata
    {
        public DirectMethodReportMetadata(string expectedSource, string actualSource, TestReportType testReportType, TimeSpan tolerancePeriod)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.TestReportType = testReportType;
            this.TolerancePeriod = tolerancePeriod;
        }

        public TimeSpan TolerancePeriod { get; }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public string[] ResultSources => new string[] { this.ExpectedSource, this.ActualSource };

        public TestReportType TestReportType { get; }

        public TestOperationResultType TestOperationResultType => TestOperationResultType.DirectMethod;
    }
}
