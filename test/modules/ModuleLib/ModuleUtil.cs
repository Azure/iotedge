// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;
    using Serilog.Events;
    using ExponentialBackoff = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.ExponentialBackoff;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    public static class ModuleUtil
    {
        public static readonly ITransientErrorDetectionStrategy DefaultTimeoutErrorDetectionStrategy =
            new DelegateErrorDetectionStrategy(ex => ex.HasTimeoutException());

        public static readonly RetryStrategy DefaultTransientRetryStrategy =
            new ExponentialBackoff(
                5,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(4));

        public static async Task<ModuleClient> CreateModuleClientAsync(
            TransportType transportType,
            ITransientErrorDetectionStrategy transientErrorDetectionStrategy = null,
            RetryStrategy retryStrategy = null,
            ILogger logger = null)
        {
            var retryPolicy = new RetryPolicy(transientErrorDetectionStrategy, retryStrategy);
            retryPolicy.Retrying += (_, args) =>
            {
                WriteLog(logger, LogLevel.Error, $"Retry {args.CurrentRetryCount} times to create module client and failed with exception:{Environment.NewLine}{args.LastException}");
            };

            ModuleClient client = await retryPolicy.ExecuteAsync(() => InitializeModuleClientAsync(transportType, logger));
            return client;
        }

        public static ILogger CreateLogger(string categoryName, LogEventLevel logEventLevel = LogEventLevel.Debug, string outputTemplate = "")
        {
            Preconditions.CheckNonWhiteSpace(categoryName, nameof(categoryName));

            var levelSwitch = new LoggingLevelSwitch(logEventLevel);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Console(outputTemplate: string.IsNullOrWhiteSpace(outputTemplate) ? "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}" : outputTemplate)
                .CreateLogger();

            return new LoggerFactory().AddSerilog().CreateLogger(categoryName);
        }

        public static string FormatMessagesTestResultValue(string trackingId, string batchId, string sequenceNumber)
        {
            return $"{trackingId};{batchId};{sequenceNumber}";
        }

        public static string FormatDirectMethodTestResultValue(string trackingId, string batchId, string sequenceNumber, string resultMessage)
        {
            return $"{trackingId};{batchId};{sequenceNumber};{resultMessage}";
        }

        public static async Task ReportStatus(TestResultCoordinatorClient.TestResultCoordinatorClient trcClient, ILogger logger, string source, string result, string format)
        {
            try
            {
                await trcClient.ReportResultAsync(new TestResultCoordinatorClient.TestOperationResult { Source = source, Result = result, CreatedAt = DateTime.UtcNow, Type = format });
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed call to report status to TestResultCoordinator");
            }
        }

        static async Task<ModuleClient> InitializeModuleClientAsync(TransportType transportType, ILogger logger)
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
            WriteLog(logger, LogLevel.Information, $"Trying to initialize module client using transport type [{transportType}].");
            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await moduleClient.OpenAsync();

            WriteLog(logger, LogLevel.Information, $"Successfully initialized module client of transport type [{transportType}].");
            return moduleClient;
        }

        static void WriteLog(ILogger logger, LogLevel logLevel, string message)
        {
            if (logger == null)
            {
                Console.WriteLine($"{logLevel}: {message}");
            }
            else
            {
                logger.Log(logLevel, message);
            }
        }
    }
}
