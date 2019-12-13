// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    /// <summary>
    /// This defines methods for a test result report generator.
    /// </summary>
    interface IReportGeneratorFactory
    {
        ITestResultReportGenerator Create(
            string trackingId,
            ITestResultComparer<TestOperationResult> testResultComparer,
            IReportMetadata reportMetadata);
    }
}
