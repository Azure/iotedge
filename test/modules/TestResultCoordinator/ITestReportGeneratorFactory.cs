// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    /// <summary>
    /// This defines methods for a test result report generator.
    /// </summary>
    interface ITestReportGeneratorFactory
    {
        ITestResultReportGenerator Create(
            string trackingId,
            IReportMetadata reportMetadata);
    }
}
