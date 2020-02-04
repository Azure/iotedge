// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System.Threading.Tasks;

    /// <summary>
    /// This defines methods for a test result report generator.
    /// </summary>
    public interface ITestResultReportGenerator
    {
        Task<ITestResultReport> CreateReportAsync();
    }
}
