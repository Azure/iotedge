// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.ReporterClients;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
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
                reportClient = ReporterClientBase.Create(
                    Logger,
                    Settings.Current.TestResultCoordinatorUrl,
                    Settings.Current.AnalyzerUrl,
                    Settings.Current.TransportType);
                // Populate ReportContent with its necessary fields
                //BEARWASHERE -- TODO: DO THIS
                TestResultReportBase report = CreateReport(reportClient, batchId);

                while (!cts.Token.IsCancellationRequested && IsTestTimeUp(testStartAt))
                {
                    (HttpStatusCode result, long dmCounter) = await directMethodClient.InvokeDirectMethodAsync(cts);

                    report
                        .SetSequenceNumber(dmCounter)
                        .SetResultMessage(result.ToString());

                    await reportClient.ReportStatus(report);

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

        public ReportContent CreateReport(ReporterClientBase reportClient, Guid batchId)
        {
            //BEARWASHERE -- TODO
            if (reportClient.GetType().Name == nameof(TestResultCoordinatorReporterClient))
            {

            }

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
