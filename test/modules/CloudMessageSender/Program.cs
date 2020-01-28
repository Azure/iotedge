// Copyright (c) Microsoft. All rights reserved.
namespace CloudMessageSender
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("CloudMessageSender");

        public static int Main() => MainAsync().Result;

        public static async Task<int> MainAsync()
        {
            Logger.LogInformation($"Starting DirectMethodSender with the following settings:\r\n{Settings.Current}");

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);
            Sender sender = null;
            TestResultReportingClient reportClient = null;

            try
            {
                Guid batchId = Guid.NewGuid();
                Logger.LogInformation($"Batch Id={batchId}");
                Logger.LogInformation($"Direct Method Sender delay start for {Settings.Current.TestStartDelay}.");
                await Task.Delay(Settings.Current.TestStartDelay, cts.Token);

                DateTime testStartAt = DateTime.UtcNow;

                sender = await Sender.CreateAsync(Settings.Current.ServiceClientConnectionString, Settings.Current.TransportType, Settings.Current.DeviceId, Logger);
                reportClient = new TestResultReportingClient() { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };

                while (!cts.Token.IsCancellationRequested && IsTestTimeUp(testStartAt))
                {
                    MessageTestResult testResult = await sender.SendCloudToDeviceMessageAsync(batchId, Settings.Current.TrackingId);

                    try
                    {
                        await ModuleUtil.ReportTestResultAsync(reportClient, Logger, testResult);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, $"Error occurred during reporting test results.");
                    }

                    await Task.Delay(Settings.Current.MessageDelay, cts.Token);
                }

                await cts.Token.WhenCanceled();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error occurred during CloudMessageSender test setup");
            }
            finally
            {
                // Implicit CloseAsync()
                sender?.Dispose();
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("CloudMessageSender Main() finished.");
            return 0;
        }

        public static bool IsTestTimeUp(DateTime testStartAt)
        {
            return (Settings.Current.TestDuration == TimeSpan.Zero) || (DateTime.UtcNow - testStartAt < Settings.Current.TestDuration);
        }
    }
}
