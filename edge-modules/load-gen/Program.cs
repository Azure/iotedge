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

        static long MessageIdCounter = 0;

        static async Task Main()
        {
            Microsoft.Extensions.Logging.ILogger logger = InitLogger().CreateLogger("loadgen");
            Log.Information($"Starting load run with the following settings:\r\n{Settings.Current.ToString()}");

            try
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

                using (var timers = new Timers())
                {
                    var random = new Random();
                    Guid batchId = Guid.NewGuid();
                    var bufferPool = new BufferPool();

                    // setup the message timer
                    timers.Add(
                        Settings.Current.MessageFrequency,
                        Settings.Current.JitterFactor,
                        () => GenMessage(client, random, batchId, bufferPool));

                    // setup the twin update timer
                    timers.Add(
                        Settings.Current.TwinUpdateFrequency,
                        Settings.Current.JitterFactor,
                        () => GenTwinUpdate(client));
                    timers.Start();

                    (
                        CancellationTokenSource cts,
                        ManualResetEventSlim completed,
                        Option<object> handler
                    ) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), logger);

                    Log.Information("Load gen running.");

                    await cts.Token.WhenCanceled();
                    Log.Information("Stopping timers.");
                    timers.Stop();
                    Log.Information("Closing connection to Edge Hub.");
                    await client.CloseAsync();
                    completed.Set();
                    handler.ForEach(h => GC.KeepAlive(h));

                    Log.Information("Load run complete. Exiting.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error occurred during load run. \r\n{ex.ToString()}");
            }
        }

        static async void GenMessage(
            ModuleClient client,
            Random random,
            Guid batchId,
            BufferPool bufferPool)
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

                long sequenceNumber = -1;
                try
                {
                    var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                    sequenceNumber = Interlocked.Increment(ref MessageIdCounter);
                    message.Properties.Add("sequenceNumber", sequenceNumber.ToString());
                    message.Properties.Add("batchId", batchId.ToString());
                    await client.SendEventAsync(Settings.Current.OutputName, message).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Log.Error($"Sequence number {sequenceNumber}, BatchId: {batchId.ToString()} {e}");
                }

            }
        }

        static async void GenTwinUpdate(ModuleClient client)
        {
            var twin = new TwinCollection();
            twin["messagesSent"] = MessageIdCounter;
            await client.UpdateReportedPropertiesAsync(twin).ConfigureAwait(false);
        }

        static ILoggerFactory InitLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            return new LoggerFactory().AddSerilog();
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
