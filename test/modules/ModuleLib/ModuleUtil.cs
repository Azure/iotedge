// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
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
            ClientOptions options,
            ITransientErrorDetectionStrategy transientErrorDetectionStrategy = null,
            RetryStrategy retryStrategy = null,
            ILogger logger = null)
        {
            var retryPolicy = new RetryPolicy(transientErrorDetectionStrategy, retryStrategy);
            retryPolicy.Retrying += (_, args) =>
            {
                WriteLog(logger, LogLevel.Error, $"Retry {args.CurrentRetryCount} times to create module client and failed with exception:{Environment.NewLine}{args.LastException}");
            };

            ModuleClient client = await retryPolicy.ExecuteAsync(() => InitializeModuleClientAsync(transportType, options, logger));
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

        public static async Task ReportTestResultAsync(TestResultReportingClient apiClient, ILogger logger, TestResultBase testResult, CancellationToken cancellationToken = default(CancellationToken))
        {
            logger.LogInformation($"Sending test result: Source={testResult.Source}, Type={testResult.ResultType}, CreatedAt={testResult.CreatedAt}, Result={testResult.GetFormattedResult()}");
            await apiClient.ReportResultAsync(testResult.ToTestOperationResultDto(), cancellationToken);
        }

        // TODO: Remove this function once the TRC support the two new endpoint properly.
        public static async Task ReportTestResultUntilSuccessAsync(TestResultReportingClient apiClient, ILogger logger, TestResultBase testResult, CancellationToken cancellationToken)
        {
            bool isSuccessful = false;
            while (!isSuccessful && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    logger.LogInformation($"Sending test result: Source={testResult.Source}, Type={testResult.ResultType}, CreatedAt={testResult.CreatedAt}, Result={testResult.GetFormattedResult()}");
                    await apiClient.ReportResultAsync(testResult.ToTestOperationResultDto(), cancellationToken);
                    isSuccessful = true;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Exception caught in ReportTestResultAsync()");
                }
            }
        }

        static async Task<ModuleClient> InitializeModuleClientAsync(TransportType transportType, ClientOptions options, ILogger logger)
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
            string modelIdIsPresent = !string.IsNullOrEmpty(options.ModelId) ? $"with modelId {options.ModelId}" : string.Empty;
            WriteLog(logger, LogLevel.Information, $"Trying to initialize module client using transport type [{transportType}] {modelIdIsPresent}.");
            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings, options);
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

        // Test info is used in the longhaul, stress, and connectivity tests to provide contextual information when reporting.
        // This info is passed from the vsts pipeline and needs to be parsed by the test modules.
        // Includes information such as build numbers, ids, host platform, etc.
        // Argument should be in the format key=value[,key=value]
        public static SortedDictionary<string, string> ParseKeyValuePairs(string keyValuePairs, ILogger logger, bool shouldBeNonEmpty)
        {
            WriteLog(logger, LogLevel.Information, $"Parsing key value pairs: {keyValuePairs}");

            Dictionary<string, string> unsortedParsedTestInfo = keyValuePairs.Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => (KeyAndValue: x, SplitIndex: x.IndexOf('=')))
                                .Where(x => x.SplitIndex >= 1)
                                .ToDictionary(
                                    x => x.KeyAndValue.Substring(0, x.SplitIndex),
                                    x => x.KeyAndValue.Substring(x.SplitIndex + 1, x.KeyAndValue.Length - x.SplitIndex - 1));

            if (shouldBeNonEmpty)
            {
                Preconditions.CheckArgument(unsortedParsedTestInfo.Count > 0, $"Key value pairs not in correct format: {keyValuePairs}");
            }

            return new SortedDictionary<string, string>(unsortedParsedTestInfo);
        }
    }
}
