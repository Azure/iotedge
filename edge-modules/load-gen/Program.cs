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
    using Message = Microsoft.Azure.Devices.Client.Message;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("LoadGen");
        static readonly Guid BatchId = Guid.NewGuid();
        static readonly string DeviceId = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");
        static readonly string ModuleId = Environment.GetEnvironmentVariable("IOTEDGE_MODULEID");
        static long messageId = 0;

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

                using (var timers = new Timers())
                {
                    Logger.LogInformation($"Batch Id={BatchId}");

                    // setup the message timer
                    timers.Add(
                        Settings.Current.MessageFrequency,
                        Settings.Current.JitterFactor,
                        () => GenerateMessageAsync(moduleClient, BatchId.ToString()));
                    timers.Start();
                    Logger.LogInformation("Load gen starting message send.");

                    (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                    await cts.Token.WhenCanceled();
                    Logger.LogInformation("Stopping timers.");
                    timers.Stop();
                    Logger.LogInformation("Closing connection to Edge Hub.");
                    await moduleClient.CloseAsync();

                    completed.Set();
                    handler.ForEach(h => GC.KeepAlive(h));
                    Logger.LogInformation("Load Gen complete. Exiting.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error occurred during load gen setup.\r\n{ex}");
            }
        }

        static async Task GenerateMessageAsync(ModuleClient client, string batchId)
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
                    Interlocked.Increment(ref messageId);
                    message.Properties.Add("sequenceNumber", messageId.ToString());
                    message.Properties.Add("batchId", batchId.ToString());

                    await client.SendEventAsync(Settings.Current.OutputName, message);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"[GenerateMessageAsync] Sequence number {messageId}, BatchId: {batchId.ToString()};{Environment.NewLine}{e}");
            }
        }
    }
}
