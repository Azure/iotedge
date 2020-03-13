// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    public abstract class TestReportMetadataBase
    {
        public TestReportMetadataBase(string testDescription)
        {
            this.TestDescription = testDescription;
        }

        public string TestDescription { get; }

        public abstract TestReportType TestReportType { get; }

        public abstract TestOperationResultType TestOperationResultType { get; }

        public override string ToString()
        {
            return $"TestDescription: {this.TestDescription}, TestReportType: {this.TestReportType}, TestOperationResultType: {this.TestOperationResultType}";
        }
    }
}
