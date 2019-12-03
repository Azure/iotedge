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

        static long messageIdCounter = 0;

        static async Task Main()
        {
            Logger.LogInformation($"Starting load gen with the following settings:\r\n{Settings.Current}");

            try
            {
                ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                Guid batchId = Guid.NewGuid();
                Logger.LogInformation($"Batch Id={batchId}");

                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                // setup the message PeriodicTask
                using (var messageTask = new PeriodicTask(
                    () => GenerateMessageAsync(moduleClient, batchId),
                    Settings.Current.MessageFrequency,
                    Settings.Current.StartDelay,
                    Logger,
                    "Generate message"))
                {
                    Logger.LogInformation("Load gen running.");
                    await cts.Token.WhenCanceled();
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

        static async Task GenerateMessageAsync(ModuleClient client, Guid batchId)
        {
            var random = new Random();
            var bufferPool = new BufferPool();

            try
            {
                using (Buffer data = bufferPool.AllocBuffer(Settings.Current.MessageSizeInBytes))
                {
                    // generate some bytes
                    random.NextBytes(data.Data);

                    // build message
                    var messageBody = new { data = data.Data };
                    var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                    messageIdCounter++;
                    message.Properties.Add("sequenceNumber", messageIdCounter.ToString());
                    message.Properties.Add("batchId", batchId.ToString());

                    await client.SendEventAsync(Settings.Current.OutputName, message);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"[GenerateMessageAsync] Sequence number {messageIdCounter}, BatchId: {batchId.ToString()};{Environment.NewLine}{e}");
            }
        }
    }
}
