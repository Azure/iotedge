// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using TestResultCoordinator.Storage;

    /// <summary>
    /// This is used to create an instance of test report generator based on given report-related parameters.
    /// </summary>
    interface ITestReportGeneratorFactory
    {
        ITestResultReportGenerator Create(
            string trackingId,
            IReportMetadata reportMetadata);
    }
}
