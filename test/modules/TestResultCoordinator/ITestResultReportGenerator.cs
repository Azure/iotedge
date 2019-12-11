// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System.Threading.Tasks;

    interface ITestResultReportGenerator
    {
        Task<ITestResultReport> CreateReportAsync();
    }
}
