// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Storage;

    static class TestReportHelper
    {
        internal static async Task<ITestResultReport[]> GenerateTestResultReports(ITestOperationResultStorage storage, ILogger logger)
        {
            logger.LogInformation($"Starting report generation for {Settings.Current.ReportMetadataList.Count} reports");

            try
            {
                var testReportGeneratorFactory = new TestReportGeneratorFactory(storage);
                var testResultReportList = new List<Task<ITestResultReport>>();
                foreach (IReportMetadata reportMetadata in Settings.Current.ReportMetadataList)
                {
                    ITestResultReportGenerator testResultReportGenerator = testReportGeneratorFactory.Create(Settings.Current.TrackingId, reportMetadata);
                    testResultReportList.Add(testResultReportGenerator.CreateReportAsync());
                }

                ITestResultReport[] testResultReports = await Task.WhenAll(testResultReportList);
                logger.LogInformation("Successfully generated all reports");

                return testResultReports;
            }
            catch (Exception ex)
            {
                logger.LogError("TestResultCoordinator failed during report generation", ex);
                return new ITestResultReport[] { };
            }
        }
    }
}
