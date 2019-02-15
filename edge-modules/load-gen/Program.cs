// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Serilog;
    using ExponentialBackoff = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.ExponentialBackoff;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    class Program
    {
        const int RetryCount = 5;

        static readonly ITransientErrorDetectionStrategy TimeoutErrorDetectionStrategy =
            new DelegateErrorDetectionStrategy(ex => ex.HasTimeoutException());

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(
                RetryCount,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(4));

        static long messageIdCounter = 0;

        static async Task Main()
        {
            ILogger logger = InitLogger().CreateLogger("loadgen");
            Log.Information($"Starting load gen with the following settings:\r\n{Settings.Current}");

            try
            {
                var client = await GetModuleClientWithRetryAsync();

                using (var timers = new Timers())
                {
                    Guid batchId = Guid.NewGuid();

                    // setup the message timer
                    timers.Add(
                        Settings.Current.MessageFrequency,
                        Settings.Current.JitterFactor,
                        () => GenerateMessageAsync(client, batchId));

                    // setup the twin update timer
                    timers.Add(
                        Settings.Current.TwinUpdateFrequency,
                        Settings.Current.JitterFactor,
                        () => GenerateTwinUpdateAsync(client, batchId));

                    timers.Start();
                    (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), logger);
                    Log.Information("Load gen running.");

                    await cts.Token.WhenCanceled();
                    Log.Information("Stopping timers.");
                    timers.Stop();
                    Log.Information("Closing connection to Edge Hub.");
                    await client.CloseAsync();
                    completed.Set();
                    handler.ForEach(h => GC.KeepAlive(h));
                    Log.Information("Load gen complete. Exiting.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error occurred during load gen.\r\n{ex}");
            }
        }

        static ILoggerFactory InitLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            return new LoggerFactory().AddSerilog();
        }

        static async Task GenerateMessageAsync(ModuleClient client, Guid batchId)
        {
            var random = new Random();
            var bufferPool = new BufferPool();
            long sequenceNumber = -1;

            try
            {
                using (Buffer data = bufferPool.AllocBuffer(Settings.Current.MessageSizeInBytes))
                {
                    // generate some bytes
                    random.NextBytes(data.Data);

                    // build message
                    var messageBody = new
                    {
                        data = data.Data,
                    };

                    var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                    sequenceNumber = Interlocked.Increment(ref messageIdCounter);
                    message.Properties.Add("sequenceNumber", sequenceNumber.ToString());
                    message.Properties.Add("batchId", batchId.ToString());
                    await client.SendEventAsync(Settings.Current.OutputName, message).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Log.Error($"[GenerateMessageAsync] Sequence number {sequenceNumber}, BatchId: {batchId.ToString()}; {e}");
            }
        }

        static async Task GenerateTwinUpdateAsync(ModuleClient client, Guid batchId)
        {
            var twin = new TwinCollection();
            long sequenceNumber = messageIdCounter;
            twin["messagesSent"] = sequenceNumber;
            try
            {
                await client.UpdateReportedPropertiesAsync(twin).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Error($"[GenerateTwinUpdateAsync] Sequence number {sequenceNumber}, BatchId: {batchId.ToString()} {e}");
            }
        }

        static async Task<ModuleClient> GetModuleClientWithRetryAsync()
        {
            var retryPolicy = new RetryPolicy(TimeoutErrorDetectionStrategy, TransientRetryStrategy);
            retryPolicy.Retrying += (_, args) =>
            {
                Log.Error($"Creating ModuleClient failed with exception {args.LastException}");
                if (args.CurrentRetryCount < RetryCount)
                {
                    Log.Information("Retrying...");
                }
            };
            ModuleClient client = await retryPolicy.ExecuteAsync(() => InitModuleClient(Settings.Current.TransportType));
            return client;
        }

        static async Task<ModuleClient> InitModuleClient(TransportType transportType)
        {
            ITransportSettings[] GetTransportSettings()
            {
                switch (transportType)
                {
                    case TransportType.Mqtt:
                    case TransportType.Mqtt_Tcp_Only:
                        return new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) };
                    case TransportType.Mqtt_WebSocket_Only:
                        return new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_WebSocket_Only) };
                    case TransportType.Amqp_WebSocket_Only:
                        return new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only) };
                    default:
                        return new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
                }
            }

            ITransportSettings[] settings = GetTransportSettings();

            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings).ConfigureAwait(false);
            await moduleClient.OpenAsync().ConfigureAwait(false);

            Log.Information("Successfully initialized module client.");
            return moduleClient;
        }
    }
}
