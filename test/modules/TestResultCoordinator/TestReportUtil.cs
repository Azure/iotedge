// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using TestResultCoordinator.Reports;
    using TestResultCoordinator.Reports.DirectMethod;

    static class TestReportUtil
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

        internal static List<ITestReportMetadata> ParseReportMetadataJson(string reportMetadataListJson, ILogger logger)
        {
            var reportMetadataList = new List<ITestReportMetadata>();

            try
            {
                JObject reportMetadatas = JObject.Parse(reportMetadataListJson);

                foreach (JToken metadata in reportMetadatas.Children())
                {
                    TestReportType testReportType = GetEnumValueFromReportMetadata<TestReportType>(metadata, "TestReportType");

                    switch (testReportType)
                    {
                        case TestReportType.CountingReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<CountingReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.TwinCountingReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<TwinCountingReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.DeploymentTestReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<DeploymentTestReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.DirectMethodReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<DirectMethodReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.NetworkControllerReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<NetworkControllerReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        default:
                            throw new NotImplementedException("{testReportType} doesn't implement to construct report metadata from Twin.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Incorrect parsing for report metadata list: {reportMetadataListJson}{Environment.NewLine}");
                throw;
            }

            return reportMetadataList;
        }

        static TEnum GetEnumValueFromReportMetadata<TEnum>(JToken metadata, string key)
        {
            return (TEnum)Enum.Parse(typeof(TEnum), ((JProperty)metadata).Value[key].ToString());
        }
    }
}
