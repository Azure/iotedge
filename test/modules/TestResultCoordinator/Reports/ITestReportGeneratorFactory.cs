// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System.Threading.Tasks;
    /// <summary>
    /// This is used to create an instance of test report generator based on given report-related parameters.
    /// </summary>
    interface ITestReportGeneratorFactory
    {
        Task<ITestResultReportGenerator> Create(
            string trackingId,
            IReportMetadata reportMetadata);
    }
}
