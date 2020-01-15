// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    /// <summary>
    /// This is used to create an instance of test report generator based on given report-related parameters.
    /// </summary>
    public interface ITestReportGeneratorFactory
    {
        ITestResultReportGenerator Create(
            string trackingId,
            ITestReportMetadata reportMetadata);
    }
}
