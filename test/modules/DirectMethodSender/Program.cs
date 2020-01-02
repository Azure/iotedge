// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.ReporterClients;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DirectMethodSender");

        public static int Main() => MainAsync().Result;

        public static async Task<int> MainAsync()
        {
            Logger.LogInformation($"Starting DirectMethodSender with the following settings:\r\n{Settings.Current}");

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);
            DirectMethodSenderBase directMethodClient = null;
            ReporterClientBase reportClient = null;

            try
            {
                Guid batchId = Guid.NewGuid();
                Logger.LogInformation($"Batch Id={batchId}");

                directMethodClient = await CreateClientAsync(Settings.Current.InvocationSource);

                Logger.LogInformation($"Load gen delay start for {Settings.Current.TestStartDelay}.");
                await Task.Delay(Settings.Current.TestStartDelay, cts.Token);

                DateTime testStartAt = DateTime.UtcNow;
                

                // BEARWASHERE: Create reportClient
                ReporterClientBase reportClient = ReporterClientBase.Create(
                    FrameworkTestType frameworkTestType, // BEARWASHERE: Introduce the test type to Settings.
                    Logger,
                    Settings.Current.TestResultCoordinatorUrl,
                    Settings.Current.AnalyzerUrl,
                    Settings.Current.TransportType);
                // BEARWASHERE: Create reportContent
                // BEARWASHERE: Deal with Source and TestOperationResultType in report setting
                ReportContent report = CreateReport(frameworkTestType);

                // ReportContent report = new ReportContent();
                // if (testReportCoordinatorUrl.HasValue)
                // {
                //     Uri baseUri = testReportCoordinatorUrl.Expect(() => new ArgumentException("testReportCoordinatorUrl is not expected to be empty"));
                //     reportClient = TestResultCoordinatorReporterClient.Create(
                //         baseUri,
                //         Logger,
                //         Settings.Current.ModuleId + ".send");
                //     report.SetTestOperationResultType(TestOperationResultType.DirectMethod);
                // }
                // else if (analyzerUrl.HasValue)
                // {
                //     Uri baseUri = analyzerUrl.Expect(() => new ArgumentException("analyzerUrl is not expected to be empty"));
                //     reportClient = TestResultCoordinatorReporterClient.Create(
                //         baseUri,
                //         Logger,
                //         Settings.Current.ModuleId + ".send");
                //     report.SetTestOperationResultType(TestOperationResultType.LegacyDirectMethod);
                // }
                // else
                // {
                //     reportClient = ModuleReporterClient.Create(
                //         Settings.Current.TransportType,
                //         Logger,
                //         Settings.Current.ModuleId + ".send");
                //     report.SetTestOperationResultType(TestOperationResultType.LegacyDirectMethod);
                // }

                while (!cts.Token.IsCancellationRequested && IsTestTimeUp(testStartAt))
                {
                    (HttpStatusCode result, long dmCounter) = await directMethodClient.InvokeDirectMethodAsync(cts);

                    report
                        .SetSequenceNumber(dmCounter)
                        .SetResultMessage(result.ToString());

                    await reportClient.ReportStatus(report);
                    // if (testReportCoordinatorUrl.HasValue)
                    // {
                    //     await testReportCoordinatorUrl.ForEachAsync(
                    //         async (Uri uri) =>
                    //         {
                    //             var testResultReportingClient = new TestResultReportingClient { BaseUrl = uri.AbsoluteUri };
                    //             await ModuleUtil.ReportStatus(
                    //                     testResultReportingClient,
                    //                     Logger,
                    //                     Settings.Current.ModuleId + ".send",
                    //                     ModuleUtil.FormatDirectMethodTestResultValue(
                    //                         Settings.Current.TrackingId.Expect(() => new ArgumentException("TrackingId is empty")),
                    //                         batchId.ToString(),
                    //                         dmCounter.ToString(),
                    //                         result.ToString()),
                    //                     TestOperationResultType.DirectMethod.ToString());
                    //         });
                    // }
                    // else
                    // {
                    //     await analyzerUrl.ForEachAsync(
                    //         async (Uri uri) =>
                    //         {
                    //             var testResultReportingClient = new TestResultReportingClient { BaseUrl = uri.AbsoluteUri };
                    //             await ReportStatus(Settings.Current.TargetModuleId, result, testResultReportingClient);
                    //         },
                    //         async () =>
                    //         {
                    //             await reportClient.SendEventAsync("AnyOutput", new Message(Encoding.UTF8.GetBytes("Direct Method call succeeded.")));
                    //         });
                    // }


                    await Task.Delay(Settings.Current.DirectMethodDelay, cts.Token);
                }

                await cts.Token.WhenCanceled();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error occurred during direct method sender test setup");
            }
            finally
            {
                // Implicit CloseAsync()
                directMethodClient?.Dispose();
                reportClient?.Dispose();
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("DirectMethodSender Main() finished.");
            return 0;
        }

        public static async Task<DirectMethodSenderBase> CreateClientAsync(InvocationSource invocationSource)
        {
            DirectMethodSenderBase directMethodClient = null;
            switch (invocationSource)
            {
                case InvocationSource.Local:
                    // Implicit OpenAsync()
                    directMethodClient = await DirectMethodLocalSender.CreateAsync(
                            Settings.Current.TransportType,
                            ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                            ModuleUtil.DefaultTransientRetryStrategy,
                            Logger);
                    break;

                case InvocationSource.Cloud:
                    // Implicit OpenAsync()
                    directMethodClient = await DirectMethodCloudSender.CreateAsync(
                            Settings.Current.ServiceClientConnectionString.Expect(() => new ArgumentException("ServiceClientConnectionString is null")),
                            (Microsoft.Azure.Devices.TransportType)Settings.Current.TransportType,
                            Logger);
                    break;

                default:
                    throw new NotImplementedException("Invalid InvocationSource type");
            }

            return directMethodClient;
        }

        public static bool IsTestTimeUp(DateTime testStartAt)
        {
            return (Settings.Current.TestDuration == TimeSpan.Zero) || (DateTime.UtcNow - testStartAt < Settings.Current.TestDuration);
        }

        static async Task ReportStatus(string moduleId, HttpStatusCode result, TestResultReportingClient apiClient)
        {
            try
            {
                await apiClient.ReportResultAsync(new TestOperationResultDto { Source = moduleId, Result = result.ToString(), CreatedAt = DateTime.UtcNow, Type = Enum.GetName(typeof(TestOperationResultType), TestOperationResultType.LegacyDirectMethod) });
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }
        }

        public ReportContent CreateReport(FrameworkTestType frameworkTestType, Guid batchId)
        {
            ReportContent report = new ReportContent();
            switch (frameworkTestType)
            {
                case FrameworkTestType.Connectivity:
                    report
                        .SetTestOperationResultType(TestOperationResultType.DirectMethod)
                        .SetTrackingId(Settings.Current.TrackingId.Expect(() => new ArgumentException("TrackingId is empty")));
                    break;

                case FrameworkTestType.EndToEnd:
                case FrameworkTestType.LongHaul:
                default:
                    report.SetTestOperationResultType(TestOperationResultType.LegacyDirectMethod);
                    break;
            }
            report
                .SetBatchId(batchId)
                .SetSource(Settings.Current.ModuleId + ".send");
            return report;
        }
    }
}
