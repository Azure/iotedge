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
        static readonly ILogger Logger = ModuleUtil.CreateLogger("CloudToDeviceMessageTester");

        public static int Main() => MainAsync().Result;

        public static async Task<int> MainAsync()
        {
            Logger.LogInformation($"Starting CloudToDeviceMessageTester with the following settings:\r\n{Settings.Current}");

            DateTime testStartAt = DateTime.UtcNow;

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            ICloudToDeviceMessageTester cloudToDeviceMessageTester = null;
            TestResultReportingClient reportClient = new TestResultReportingClient() { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
            try
            {
                if (Settings.Current.TestMode == CloudToDeviceMessageTesterMode.Receiver)
                {
                    cloudToDeviceMessageTester = new CloudToDeviceMessageReceiver(
                        Logger,
                        Settings.Current.SharedSettings,
                        Settings.Current.ReceiverSettings,
                        reportClient);
                }
                else
                {
                    cloudToDeviceMessageTester = new CloudToDeviceMessageSender(
                        Logger,
                        Settings.Current.SharedSettings,
                        Settings.Current.SenderSettings,
                        reportClient);
                }

                await cloudToDeviceMessageTester.StartAsync(cts.Token);
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

            await cts.Token.WhenCanceled();
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation($"{nameof(Program.MainAsync)} finished.");
            return 0;
        }
    }
}
