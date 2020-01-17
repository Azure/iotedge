// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    static class TestReportHelper
    {
        internal static async Task<ITestResultReport[]> GenerateTestResultReportsAsync(
            string trackingId,
            List<ITestReportMetadata> reportMetadatalist,
            ITestReportGeneratorFactory testReportGeneratorFactory,
            ILogger logger)
        {
            Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            Preconditions.CheckNotNull(reportMetadatalist, nameof(reportMetadatalist));

            logger.LogInformation($"Starting report generation for {reportMetadatalist.Count} reports");

            var testResultReportTasks = new List<Task<ITestResultReport>>();

            try
            {
                foreach (ITestReportMetadata reportMetadata in reportMetadatalist)
                {
                    ITestResultReportGenerator testResultReportGenerator = await testReportGeneratorFactory.CreateAsync(trackingId, reportMetadata);
                    testResultReportTasks.Add(testResultReportGenerator.CreateReportAsync());
                }

                ITestResultReport[] testResultReports = await Task.WhenAll(testResultReportTasks);
                logger.LogInformation("Successfully generated all reports");

                return testResultReports;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "At least 1 report generation is failed.");
                var reports = new List<ITestResultReport>();

                foreach (Task<ITestResultReport> reportTask in testResultReportTasks)
                {
                    if (reportTask.IsFaulted)
                    {
                        logger.LogError(reportTask.Exception, "Error in report generation task");
                    }
                    else
                    {
                        reports.Add(reportTask.Result);
                    }
                }

                return reports.ToArray();
            }
        }
    }
}
