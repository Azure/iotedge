// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics.Tracing;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DirectMethodSender");
        public static int Main() => MainAsync().Result;

	    private static readonly ConsoleEventListener _listener = new ConsoleEventListener();

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
                Logger.LogInformation($"DirectMethodSender delay start for {Settings.Current.TestStartDelay}.");
                await Task.Delay(Settings.Current.TestStartDelay, cts.Token);

                DateTime testStartAt = DateTime.UtcNow;

                directMethodClient = await CreateClientAsync(Settings.Current.InvocationSource);
                reportClient = ReporterClientBase.Create(
                    Logger,
                    Settings.Current.ReportingEndpointUrl,
                    Settings.Current.TransportType);

                while (!cts.Token.IsCancellationRequested && IsTestTimeUp(testStartAt))
                {
                    await Task.Delay(Settings.Current.DirectMethodFrequency, cts.Token);

                    (HttpStatusCode resultStatusCode, ulong dmCounter) = await directMethodClient.InvokeDirectMethodAsync(Settings.Current.DirectMethodName, cts);
                    DirectMethodResultType resultType = Settings.Current.DirectMethodResultType;

                    if (ShouldReportResults(resultType, resultStatusCode))
                    {
                        // Generate a testResult type depending on the reporting endpoint
                        TestResultBase testResult = ConstructTestResult(
                            resultType,
                            batchId,
                            dmCounter,
                            resultStatusCode);

                        await reportClient.SendTestResultAsync(testResult);
                    }
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

        // Create reporting result depending on which endpoint is being used.
        public static TestResultBase ConstructTestResult(DirectMethodResultType directMethodResultType, Guid batchId, ulong counter, HttpStatusCode result)
        {
            string source = Settings.Current.ModuleId + ".send";
            switch (directMethodResultType)
            {
                case DirectMethodResultType.DirectMethodTestResult:
                    return new DirectMethodTestResult(
                        source,
                        DateTime.UtcNow,
                        Settings.Current.TrackingId.Expect(() => new ArgumentException("TrackingId is empty")),
                        batchId,
                        counter,
                        result);

                case DirectMethodResultType.LegacyDirectMethodTestResult:
                    return new LegacyDirectMethodTestResult(
                        source,
                        DateTime.UtcNow,
                        result.ToString());

                default:
                    throw new NotImplementedException("Reporting Endpoint has an unknown type");
            }
        }

        static bool ShouldReportResults(DirectMethodResultType resultType, HttpStatusCode statusCode)
        {
            return !(resultType == DirectMethodResultType.LegacyDirectMethodTestResult && statusCode == HttpStatusCode.NotFound);
        }
    }
}
