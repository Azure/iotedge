// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Timer;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Core.Constants;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using ISinkResult = Microsoft.Azure.Devices.Routing.Core.ISinkResult<Microsoft.Azure.Devices.Routing.Core.IMessage>;
    using SystemProperties = Microsoft.Azure.Devices.Edge.Hub.Core.SystemProperties;

    public class CloudEndpoint : Endpoint
    {
        readonly Func<string, Task<Util.Option<ICloudProxy>>> cloudProxyGetterFunc;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;
        readonly int maxBatchSize;

        public CloudEndpoint(
            string id,
            Func<string, Task<Util.Option<ICloudProxy>>> cloudProxyGetterFunc,
            Core.IMessageConverter<IRoutingMessage> messageConverter,
            int maxBatchSize = 10,
            int fanoutFactor = 10)
            : base(id)
        {
            Preconditions.CheckArgument(maxBatchSize > 0, "MaxBatchSize should be greater than 0");
            this.cloudProxyGetterFunc = Preconditions.CheckNotNull(cloudProxyGetterFunc);
            this.messageConverter = Preconditions.CheckNotNull(messageConverter);
            this.maxBatchSize = maxBatchSize;
            this.FanOutFactor = fanoutFactor;
            Events.Created(id, maxBatchSize, fanoutFactor);
        }

        public override string Type => this.GetType().Name;

        public override int FanOutFactor { get; }

        public override IProcessor CreateProcessor() => new CloudMessageProcessor(this);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
            // TODO - No-op for now
        }

        internal class CloudMessageProcessor : IProcessor
        {
            static readonly ISet<Type> RetryableExceptions = new HashSet<Type>
            {
                typeof(TimeoutException),
                typeof(IOException),
                typeof(IotHubException),
                typeof(UnauthorizedException) // This indicates the SAS token has expired, and will get a new one.
            };

            readonly CloudEndpoint cloudEndpoint;

            public CloudMessageProcessor(CloudEndpoint endpoint)
            {
                this.cloudEndpoint = Preconditions.CheckNotNull(endpoint);
            }

            public Endpoint Endpoint => this.cloudEndpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(this.IsTransientException);

            public async Task<ISinkResult> ProcessAsync(IRoutingMessage routingMessage, CancellationToken token)
            {
                Preconditions.CheckNotNull(routingMessage, nameof(routingMessage));

                string id = this.GetIdentity(routingMessage);
                ISinkResult result = await this.ProcessClientMessagesBatch(id, new List<IRoutingMessage> { routingMessage }, token);
                Events.DoneProcessing(token);
                return result;
            }

            public Task<ISinkResult> ProcessAsync(ICollection<IRoutingMessage> routingMessages, CancellationToken token)
            {
                Events.ProcessingMessages(Preconditions.CheckNotNull(routingMessages, nameof(routingMessages)));
                Task<ISinkResult> syncResult = this.ProcessByClients(routingMessages, token);
                Events.DoneProcessing(token);
                return syncResult;
            }

            public Task CloseAsync(CancellationToken token) => Task.CompletedTask;

            internal static int GetBatchSize(int batchSize, long messageSize) =>
                Math.Min((int)(Constants.MaxMessageSize / messageSize), batchSize);

            static bool IsRetryable(Exception ex) => ex != null && RetryableExceptions.Any(re => re.IsInstanceOfType(ex));

            static ISinkResult HandleNoIdentity(List<IRoutingMessage> routingMessages)
            {
                Events.InvalidMessageNoIdentity();
                return GetSyncResultForInvalidMessages(new InvalidOperationException("Message does not contain device id"), routingMessages);
            }

            static ISinkResult HandleNoConnection(string identity, List<IRoutingMessage> routingMessages)
            {
                Events.IoTHubNotConnected(identity);
                return GetSyncResultForFailedMessages(new EdgeHubConnectionException($"Could not get connection to IoT Hub for {identity}"), routingMessages);
            }

            static ISinkResult HandleCancelled(List<IRoutingMessage> routingMessages)
                => GetSyncResultForFailedMessages(new EdgeHubConnectionException($"Cancelled sending messages to IotHub"), routingMessages);

            static ISinkResult GetSyncResultForFailedMessages(Exception ex, List<IRoutingMessage> routingMessages)
            {
                var sendFailureDetails = new SendFailureDetails(FailureKind.Transient, ex);
                return new SinkResult<IRoutingMessage>(ImmutableList<IRoutingMessage>.Empty, routingMessages, sendFailureDetails);
            }

            static ISinkResult GetSyncResultForInvalidMessages(Exception ex, List<IRoutingMessage> routingMessages)
            {
                List<InvalidDetails<IRoutingMessage>> invalid = routingMessages
                    .Select(m => new InvalidDetails<IRoutingMessage>(m, FailureKind.InvalidInput))
                    .ToList();
                var sendFailureDetails = new SendFailureDetails(FailureKind.InvalidInput, ex);
                return new SinkResult<IRoutingMessage>(ImmutableList<IRoutingMessage>.Empty, ImmutableList<IRoutingMessage>.Empty, invalid, sendFailureDetails);
            }

            async Task<ISinkResult> ProcessByClients(ICollection<IRoutingMessage> routingMessages, CancellationToken token)
            {
                var routingMessageGroups = (from r in routingMessages
                                            group r by this.GetIdentity(r)
                                            into g
                                            select new { Id = g.Key, RoutingMessages = g.ToList() })
                    .ToList();

                var succeeded = new List<IRoutingMessage>();
                var failed = new List<IRoutingMessage>();
                var invalid = new List<InvalidDetails<IRoutingMessage>>();
                Option<SendFailureDetails> sendFailureDetails =
                    Option.None<SendFailureDetails>();

                Events.ProcessingMessageGroups(routingMessages, routingMessageGroups.Count, this.cloudEndpoint.FanOutFactor);

                foreach (var groupBatch in routingMessageGroups.Batch(this.cloudEndpoint.FanOutFactor))
                {
                    IEnumerable<Task<ISinkResult<IRoutingMessage>>> sendTasks = groupBatch
                        .Select(item => this.ProcessClientMessages(item.Id, item.RoutingMessages, token));
                    ISinkResult<IRoutingMessage>[] sinkResults = await Task.WhenAll(sendTasks);
                    foreach (ISinkResult<IRoutingMessage> res in sinkResults)
                    {
                        succeeded.AddRange(res.Succeeded);
                        failed.AddRange(res.Failed);
                        invalid.AddRange(res.InvalidDetailsList);
                        // Different branches could have different results, but only the most significant will be reported
                        if (IsMoreSignificant(sendFailureDetails, res.SendFailureDetails))
                        {
                            sendFailureDetails = res.SendFailureDetails;
                        }
                    }
                }

                return new SinkResult<IRoutingMessage>(
                    succeeded,
                    failed,
                    invalid,
                    sendFailureDetails.GetOrElse(default(SendFailureDetails)));
            }

            // Process all messages for a particular client
            async Task<ISinkResult<IRoutingMessage>> ProcessClientMessages(string id, List<IRoutingMessage> routingMessages, CancellationToken token)
            {
                var succeeded = new List<IRoutingMessage>();
                var failed = new List<IRoutingMessage>();
                var invalid = new List<InvalidDetails<IRoutingMessage>>();
                Option<SendFailureDetails> sendFailureDetails =
                    Option.None<SendFailureDetails>();

                // Find the maximum message size, and divide messages into largest batches
                // not exceeding max allowed IoTHub message size.
                long maxMessageSize = routingMessages.Select(r => r.Size()).Max();
                int batchSize = GetBatchSize(Math.Min(this.cloudEndpoint.maxBatchSize, routingMessages.Count), maxMessageSize);
                foreach (IEnumerable<IRoutingMessage> batch in routingMessages.Batch(batchSize))
                {
                    ISinkResult res = await this.ProcessClientMessagesBatch(id, batch.ToList(), token);
                    succeeded.AddRange(res.Succeeded);
                    failed.AddRange(res.Failed);
                    invalid.AddRange(res.InvalidDetailsList);

                    if (IsMoreSignificant(sendFailureDetails, res.SendFailureDetails))
                    {
                        sendFailureDetails = res.SendFailureDetails;
                    }
                }

                return new SinkResult<IRoutingMessage>(
                    succeeded,
                    failed,
                    invalid,
                    sendFailureDetails.GetOrElse(default(SendFailureDetails)));
            }

            static bool IsMoreSignificant(Option<SendFailureDetails> baseDetails, Option<SendFailureDetails> currentDetails)
            {
                // whatever happend before, if no details now, that cannot be more significant
                if (currentDetails == Option.None<SendFailureDetails>())
                    return false;

                // if something wrong happened now, but nothing before, then that is more significant
                if (baseDetails == Option.None<SendFailureDetails>())
                    return true;

                // at this point something has happened before, as well as now. Pick the more significant
                var baseUnwrapped = baseDetails.Expect(ThrowBadProgramLogic);
                var currentUnwrapped = currentDetails.Expect(ThrowBadProgramLogic);

                // in theory this case is represened by Option.None and handled earlier, but let's check it just for sure
                if (currentUnwrapped.FailureKind == FailureKind.None)
                    return false;

                // Transient beats non-transient
                if (baseUnwrapped.FailureKind != FailureKind.Transient && currentUnwrapped.FailureKind == FailureKind.Transient)
                    return true;

                return false;

                InvalidOperationException ThrowBadProgramLogic() => new InvalidOperationException("Error in program logic, uwrapped Option<T> should have had value");
            }

            async Task<ISinkResult<IRoutingMessage>> ProcessClientMessagesBatch(string id, List<IRoutingMessage> routingMessages, CancellationToken token)
            {
                if (string.IsNullOrEmpty(id))
                {
                    return HandleNoIdentity(routingMessages);
                }

                if (token.IsCancellationRequested)
                {
                    return HandleCancelled(routingMessages);
                }

                Util.Option<ICloudProxy> cloudProxy = await this.cloudEndpoint.cloudProxyGetterFunc(id);
                ISinkResult result = await cloudProxy.Match(
                    async cp =>
                    {
                        try
                        {
                            List<IMessage> messages = routingMessages
                                .Select(r => this.cloudEndpoint.messageConverter.ToMessage(r))
                                .ToList();

                            using (MetricsV0.CloudLatency(id))
                            {
                                if (messages.Count == 1)
                                {
                                    await cp.SendMessageAsync(messages[0]);
                                }
                                else
                                {
                                    await cp.SendMessageBatchAsync(messages);
                                }
                            }

                            MetricsV0.MessageCount(id, messages.Count);

                            return new SinkResult<IRoutingMessage>(routingMessages);
                        }
                        catch (Exception ex)
                        {
                            return this.HandleException(ex, id, routingMessages);
                        }
                    },
                    () => Task.FromResult(HandleNoConnection(id, routingMessages)));

                return result;
            }

            ISinkResult HandleException(Exception ex, string id, List<IRoutingMessage> routingMessages)
            {
                if (IsRetryable(ex))
                {
                    Events.RetryingMessage(id, ex);
                    return GetSyncResultForFailedMessages(new EdgeHubIOException($"Error sending messages to IotHub for device {this.cloudEndpoint.Id}"), routingMessages);
                }
                else
                {
                    Events.InvalidMessage(id, ex);
                    return GetSyncResultForInvalidMessages(ex, routingMessages);
                }
            }

            bool IsTransientException(Exception ex) => ex is EdgeHubIOException || ex is EdgeHubConnectionException;

            string GetIdentity(IRoutingMessage routingMessage)
            {
                if (routingMessage.SystemProperties.TryGetValue(SystemProperties.ConnectionDeviceId, out string deviceId))
                {
                    return routingMessage.SystemProperties.TryGetValue(SystemProperties.ConnectionModuleId, out string moduleId)
                        ? $"{deviceId}/{moduleId}"
                        : deviceId;
                }

                Events.DeviceIdNotFound(routingMessage);
                return string.Empty;
            }
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.CloudEndpoint;
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudEndpoint>();

            enum EventIds
            {
                DeviceIdNotFound = IdStart,
                IoTHubNotConnected,
                RetryingMessages,
                InvalidMessage,
                ProcessingMessages,
                InvalidMessageNoIdentity,
                CancelledProcessing,
                Created,
                DoneProcessing
            }

            public static void DeviceIdNotFound(IRoutingMessage routingMessage)
            {
                string message = routingMessage.SystemProperties.TryGetValue(SystemProperties.MessageId, out string messageId)
                    ? Invariant($"Message with MessageId {messageId} does not contain a device Id.")
                    : "Received message does not contain a device Id";
                Log.LogWarning((int)EventIds.DeviceIdNotFound, message);
            }

            public static void ProcessingMessages(ICollection<IRoutingMessage> routingMessages)
            {
                Log.LogDebug((int)EventIds.ProcessingMessages, Invariant($"Sending {routingMessages.Count} message(s) upstream."));
            }

            public static void CancelledProcessingMessages(ICollection<IRoutingMessage> messages)
            {
                if (messages.Count > 0)
                {
                    IRoutingMessage firstMessage = messages.OrderBy(m => m.Offset).First();
                    Log.LogDebug((int)EventIds.CancelledProcessing, $"Cancelled sending messages from offset {firstMessage.Offset}");
                }
                else
                {
                    Log.LogDebug((int)EventIds.CancelledProcessing, "Cancelled sending messages");
                }
            }

            public static void CancelledProcessingMessage(IRoutingMessage message)
            {
                Log.LogDebug((int)EventIds.CancelledProcessing, $"Cancelled sending messages from offset {message.Offset}");
            }

            public static void InvalidMessageNoIdentity()
            {
                Log.LogWarning((int)EventIds.InvalidMessageNoIdentity, "Cannot process message with no identity, discarding it.");
            }

            public static void ProcessingMessageGroups(ICollection<IRoutingMessage> routingMessages, int groups, int fanoutFactor)
            {
                Log.LogDebug((int)EventIds.ProcessingMessages, Invariant($"Sending {routingMessages.Count} message(s) upstream, divided into {groups} groups. Processing maximum {fanoutFactor} groups in parallel."));
            }

            public static void Created(string id, int maxbatchSize, int fanoutFactor)
            {
                Log.LogInformation((int)EventIds.Created, Invariant($"Created cloud endpoint {id} with max batch size {maxbatchSize} and fan-out factor of {fanoutFactor}."));
            }

            public static void DoneProcessing(CancellationToken token)
            {
                if (token.IsCancellationRequested)
                {
                    Log.LogInformation((int)EventIds.CancelledProcessing, "Stopped sending messages to upstream as the operation was cancelled");
                }
                else
                {
                    Log.LogDebug((int)EventIds.DoneProcessing, "Finished processing messages to upstream");
                }
            }

            internal static void IoTHubNotConnected(string id)
            {
                Log.LogWarning((int)EventIds.IoTHubNotConnected, Invariant($"Could not get an active Iot Hub connection for client {id}"));
            }

            internal static void RetryingMessage(string id, Exception ex)
            {
                Log.LogDebug((int)EventIds.RetryingMessages, Invariant($"Retrying sending message from {id} to Iot Hub due to exception {ex.GetType()}:{ex.Message}."));
            }

            internal static void InvalidMessage(string id, Exception ex)
            {
                Log.LogWarning((int)EventIds.InvalidMessage, ex, Invariant($"Non retryable exception occurred while sending message for client {id}."));
            }
        }

        static class MetricsV0
        {
            static readonly CounterOptions EdgeHubToCloudMessageCountOptions = new CounterOptions
            {
                Name = "EdgeHubToCloudMessageSentCount",
                MeasurementUnit = Unit.Events,
                ResetOnReporting = true,
            };

            static readonly TimerOptions EdgeHubToCloudMessageLatencyOptions = new TimerOptions
            {
                Name = "EdgeHubToCloudMessageLatencyMs",
                MeasurementUnit = Unit.None,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds
            };

            public static void MessageCount(string identity, int count)
                => Util.Metrics.MetricsV0.CountIncrement(GetTags(identity), EdgeHubToCloudMessageCountOptions, count);

            public static IDisposable CloudLatency(string identity)
                => Util.Metrics.MetricsV0.Latency(GetTags(identity), EdgeHubToCloudMessageLatencyOptions);

            static MetricTags GetTags(string id)
            {
                return new MetricTags("DeviceId", id);
            }
        }
    }
}
