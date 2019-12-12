// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
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

                moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                Logger.LogInformation($"Load gen delay start for {Settings.Current.TestStartDelay}.");
                await Task.Delay(Settings.Current.TestStartDelay);

                DateTime testStartAt = DateTime.UtcNow;
                long messageIdCounter = 1;
                while (!cts.IsCancellationRequested &&
                    (Settings.Current.TestDuration == TimeSpan.Zero || DateTime.UtcNow - testStartAt < Settings.Current.TestDuration))
                {
                    try
                    {
                        await SendEventAsync(moduleClient, batchId, Settings.Current.TrackingId, messageIdCounter);
                        messageIdCounter++;
                        await Task.Delay(Settings.Current.MessageFrequency);

                        if (messageIdCounter % 1000 == 0)
                        {
                            Logger.LogInformation($"Sent {messageIdCounter} messages.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, $"[SendEventAsync] Sequence number {messageIdCounter}, BatchId: {batchId.ToString()};");
                    }
                }

                Logger.LogInformation("Closing connection to Edge Hub.");
                await moduleClient.CloseAsync();

                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred during load gen.");
                moduleClient?.Dispose();
            }

            Logger.LogInformation("Load Gen complete. Exiting.");
        }

        static async Task SendEventAsync(ModuleClient client, Guid batchId, string trackingId, long messageId)
        {
            var random = new Random();
            var bufferPool = new BufferPool();

            using (Buffer data = bufferPool.AllocBuffer(Settings.Current.MessageSizeInBytes))
            {
                // generate some bytes
                random.NextBytes(data.Data);

                // build message
                var messageBody = new { data = data.Data };
                var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                message.Properties.Add("sequenceNumber", messageId.ToString());
                message.Properties.Add("batchId", batchId.ToString());
                message.Properties.Add("trackingId", trackingId);

                await client.SendEventAsync(Settings.Current.OutputName, message);
            }
        }
    }
}
