// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using TestResultCoordinator.Reports;
    using TestResultCoordinator.Reports.DirectMethod.Connectivity;
    using TestResultCoordinator.Reports.DirectMethod.LongHaul;
    using TestResultCoordinator.Reports.EdgeHubRestartTest;
    using TestResultCoordinator.Reports.LegacyTwin;

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
                        case TestReportType.EdgeHubRestartDirectMethodReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.EdgeHubRestartMessageReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<EdgeHubRestartMessageReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.TwinCountingReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<TwinCountingReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.LegacyTwinReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<LegacyTwinReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.DeploymentTestReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<DeploymentTestReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.DirectMethodConnectivityReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<DirectMethodConnectivityReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.DirectMethodLongHaulReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<DirectMethodLongHaulReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.NetworkControllerReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<NetworkControllerReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.ErrorReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<ErrorReportMetadata>(((JProperty)metadata).Value.ToString()));
                            break;
                        case TestReportType.TestInfoReport:
                            reportMetadataList.Add(JsonConvert.DeserializeObject<TestInfoReportMetadata>(((JProperty)metadata).Value.ToString()));
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

        internal static async Task<Uri> GetOrCreateBlobContainerSasUriForLogAsync(string storageAccountConnectionString)
        {
            string containerName = GetAzureBlobContainerNameForLog();
            var containerClient = new BlobContainerClient(storageAccountConnectionString, containerName);

            if (!await containerClient.ExistsAsync())
            {
                await containerClient.CreateAsync();
            }

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageAccountConnectionString);
            var container = new CloudBlobContainer(containerClient.Uri, storageAccount.Credentials);
            return GetContainerSasUri(container);
        }

        internal static async Task UploadLogsAsync(string iotHubConnectionString, Uri blobContainerWriteUri, ILogger logger)
        {
            Preconditions.CheckNonWhiteSpace(iotHubConnectionString, nameof(iotHubConnectionString));
            Preconditions.CheckNotNull(blobContainerWriteUri, nameof(blobContainerWriteUri));
            Preconditions.CheckNotNull(logger, nameof(logger));

            DateTime uploadLogStartAt = DateTime.UtcNow;
            logger.LogInformation("Send upload logs request to edgeAgent.");

            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
            CloudToDeviceMethod uploadLogRequest =
                new CloudToDeviceMethod("UploadModuleLogs")
                    .SetPayloadJson($"{{ \"schemaVersion\": \"1.0\", \"sasUrl\": \"{blobContainerWriteUri.AbsoluteUri}\", \"items\": [{{ \"id\": \".*\", \"filter\": {{}} }}], \"encoding\": \"none\",\"content-type\": \"text\" }}");

            CloudToDeviceMethodResult uploadLogResponse = await serviceClient.InvokeDeviceMethodAsync(Settings.Current.DeviceId, "$edgeAgent", uploadLogRequest);

            (string status, string correlationId) = GetUploadLogResponseResult(uploadLogResponse.GetPayloadAsJson());
            logger.LogInformation($"Upload logs response: status={status}, correlationId={correlationId}");

            int checkUploadStatusPeriod = 60 * 1000;    // 1 min
            while (!string.Equals(status, UploadLogResponseStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(checkUploadStatusPeriod);
                CloudToDeviceMethod getTaskStatusRequest =
                new CloudToDeviceMethod("GetTaskStatus")
                    .SetPayloadJson($"{{ \"schemaVersion\": \"1.0\", \"correlationId\": \"{correlationId}\" }}");
                CloudToDeviceMethodResult getTaskStatusResponse = await serviceClient.InvokeDeviceMethodAsync(Settings.Current.DeviceId, "$edgeAgent", getTaskStatusRequest);
                (status, _) = GetUploadLogResponseResult(getTaskStatusResponse.GetPayloadAsJson());
            }

            // Complete upload log to Azure blob
            DateTime uploadLogFinishAt = DateTime.UtcNow;
            logger.LogInformation($"Upload logs was started at {uploadLogStartAt} and completed at {uploadLogFinishAt}; and took {uploadLogFinishAt - uploadLogStartAt}.");
        }

        internal static void EnqueueAndEnforceMaxSize<T>(Queue<T> q, T result, ushort maxSize)
        {
            q.Enqueue(result);
            if (q.Count > maxSize)
            {
                q.Dequeue();
            }
        }

        static TEnum GetEnumValueFromReportMetadata<TEnum>(JToken metadata, string key)
        {
            return (TEnum)Enum.Parse(typeof(TEnum), ((JProperty)metadata).Value[key].ToString());
        }

        static (string status, string correlationId) GetUploadLogResponseResult(string responseJson)
        {
            IEnumerable<UploadLogResponseStatus> validResponseStatuses = Enum.GetValues(typeof(UploadLogResponseStatus)).Cast<UploadLogResponseStatus>();

            JObject responseObj = JObject.Parse(responseJson);
            string status = ((JValue)responseObj["status"]).ToString();
            string correlationId = ((JValue)responseObj["correlationId"]).ToString();

            if (!validResponseStatuses.Any(s => string.Equals(s.ToString(), status, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ApplicationException($"Upload log response status is {status} with correlation id {correlationId}, which is invalid.");
            }

            return (status, correlationId);
        }

        static string GetAzureBlobContainerNameForLog()
        {
            return $"logs{DateTime.UtcNow.ToString("yyyyMMdd")}";
        }

        static Uri GetContainerSasUri(CloudBlobContainer container)
        {
            var adHocPolicy = new SharedAccessBlobPolicy()
            {
                // When the start time for the SAS is omitted, the start time is assumed to be the time when the storage service receives the request.
                // Omitting the start time for a SAS that is effective immediately helps to avoid clock skew.
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1),
                Permissions = SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.List
            };

            string sasContainerToken = container.GetSharedAccessSignature(adHocPolicy, null);
            return new Uri(container.Uri + sasContainerToken);
        }

        enum UploadLogResponseStatus
        {
            NotStarted,
            Running,
            Completed
        }
    }
}
