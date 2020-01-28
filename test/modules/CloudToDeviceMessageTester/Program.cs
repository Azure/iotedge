// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("CloudMessageSender");

        public static int Main() => MainAsync().Result;

        public static async Task<int> MainAsync()
        {
            Logger.LogInformation($"Starting CloudToDeviceMessageTester with the following settings:\r\n{Settings.Current}");

            DateTime testStartAt = DateTime.UtcNow;

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            CloudToDeviceMessageTesterBase cloudToDeviceMessageTester = null;
            TestResultReportingClient reportClient = new TestResultReportingClient() { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
            try
            {
                if (Settings.Current.TestMode == CloudToDeviceMessageTesterMode.Receiver)
                {
                    cloudToDeviceMessageTester = new Receiver(
                        Logger,
                        Settings.Current.IoTHubConnectionString,
                        Settings.Current.DeviceId,
                        Settings.Current.ModuleId,
                        Settings.Current.TransportType,
                        Settings.Current.TestDuration,
                        reportClient);
                }
                else
                {
                    cloudToDeviceMessageTester = new Sender(
                        Logger,
                        Settings.Current.IoTHubConnectionString,
                        Settings.Current.DeviceId,
                        Settings.Current.ModuleId,
                        Settings.Current.TransportType,
                        Settings.Current.TestDuration,
                        reportClient,
                        Settings.Current.TrackingId,
                        Settings.Current.MessageDelay,
                        Settings.Current.TestStartDelay);
                }

                await cloudToDeviceMessageTester.InitAsync(cts, testStartAt);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error occurred during CloudToDeviceMessageTester while in {Settings.Current.TestMode} mode.");
            }
            finally
            {
                // Implicit CloseAsync()
                cloudToDeviceMessageTester?.Dispose();
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("CloudToDeviceMessageTester Main() finished.");
            return 0;
        }
    }
}
