// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("LoadGen");

        static async Task Main()
        {
            Logger.LogInformation($"Starting load gen with the following settings:\r\n{Settings.Current}");

            ModuleClient moduleClient = null;

            try
            {
                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                Guid batchId = Guid.NewGuid();
                Logger.LogInformation($"Batch Id={batchId}");

                ClientOptions options = new ClientOptions();
                Settings.Current.ModelId.ForEach(m => options.ModelId = m);
                moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    options,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                Logger.LogInformation($"Load gen delay start for {Settings.Current.TestStartDelay}.");
                await Task.Delay(Settings.Current.TestStartDelay);

                LoadGenSenderBase sender;
                switch (Settings.Current.SenderType)
                {
                    case LoadGenSenderType.PriorityMessageSender:
                        sender = new PriorityMessageSender(Logger, moduleClient, batchId, Settings.Current.TrackingId);
                        break;
                    case LoadGenSenderType.DefaultSender:
                    default:
                        sender = new DefaultMessageSender(Logger, moduleClient, batchId, Settings.Current.TrackingId);
                        break;
                }

                DateTime testStartAt = DateTime.UtcNow;
                await sender.RunAsync(cts, testStartAt);

                Logger.LogInformation("Finish sending messages.");
                await cts.Token.WhenCanceled();
                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred during load gen.");
            }
            finally
            {
                Logger.LogInformation("Closing connection to Edge Hub.");
                moduleClient?.CloseAsync();
                moduleClient?.Dispose();
            }

            Logger.LogInformation("Load Gen complete. Exiting.");
        }
    }
}
