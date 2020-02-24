// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Newtonsoft.Json;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

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
                Logger.LogInformation($"Load gen delay start for {Settings.Current.StartDelay}.");
                await Task.Delay(Settings.Current.StartDelay);

                long messageIdCounter = 1;
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        await SendEventAsync(moduleClient, batchId, messageIdCounter);
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
                Logger.LogInformation("Load Gen complete. Exiting.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error occurred during load gen.\r\n{ex}");
            }
        }

        static async Task SendEventAsync(ModuleClient client, Guid batchId, long messageId)
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

                await client.SendEventAsync(Settings.Current.OutputName, message);
            }
        }
    }
}
