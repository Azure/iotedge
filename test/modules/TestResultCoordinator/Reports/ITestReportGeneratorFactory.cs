// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System.Threading.Tasks;

    /// <summary>
    /// This is used to create an instance of test report generator based on given report-related parameters.
    /// </summary>
    public interface ITestReportGeneratorFactory
    {
        Task<ITestResultReportGenerator> CreateAsync(
            string trackingId,
            ITestReportMetadata reportMetadata);
    }
}
