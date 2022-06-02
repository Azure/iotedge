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
        readonly Func<string, Task<Try<ICloudProxy>>> cloudProxyGetterFunc;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;
        readonly int maxBatchSize;
        readonly bool trackDeviceState;

        public CloudEndpoint(
            string id,
            Func<string, Task<Try<ICloudProxy>>> cloudProxyGetterFunc,
            Core.IMessageConverter<IRoutingMessage> messageConverter,
            bool trackDeviceState,
            int maxBatchSize = 10,
            int fanoutFactor = 10)
            : base(id)
        {
            Preconditions.CheckArgument(maxBatchSize > 0, "MaxBatchSize should be greater than 0");
            this.cloudProxyGetterFunc = Preconditions.CheckNotNull(cloudProxyGetterFunc);
            this.messageConverter = Preconditions.CheckNotNull(messageConverter);
            this.trackDeviceState = trackDeviceState;
            this.maxBatchSize = maxBatchSize;
            this.FanOutFactor = fanoutFactor;
            Events.Created(id, maxBatchSize, fanoutFactor);
        }

        public override string Type => this.GetType().Name;

        public override int FanOutFactor { get; }

        public override IProcessor CreateProcessor() => new CloudMessageProcessor(this, this.trackDeviceState);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
            // TODO - No-op for now
        }

        internal class CloudMessageProcessor : IProcessor
        {
            readonly ISet<Type> retryableExceptions = new HashSet<Type>
            {
                typeof(TimeoutException),
                typeof(IOException),
                typeof(IotHubException),
                typeof(UnauthorizedException) // This indicates the SAS token has expired, and will get a new one.
            };

            readonly CloudEndpoint cloudEndpoint;
            readonly bool trackDeviceState;

            public CloudMessageProcessor(CloudEndpoint endpoint, bool trackDeviceState)
            {
                this.cloudEndpoint = Preconditions.CheckNotNull(endpoint);
                this.trackDeviceState = trackDeviceState;

                if (!trackDeviceState)
                {
                    this.retryableExceptions.Add(typeof(DeviceInvalidStateException));
                }
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
                Math.Min((int)(Constants.MaxMessageSize / Math.Max(1, messageSize)), batchSize);

            bool IsRetryable(Exception ex) => ex != null && this.retryableExceptions.Any(re => re.IsInstanceOfType(ex));

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
                var result = new MergingSinkResult<IRoutingMessage>();

                var routingMessageGroups = (from r in routingMessages
                                            group r by this.GetIdentity(r)
                                            into g
                                            select new { Id = g.Key, RoutingMessages = g.ToList() })
                    .ToList();

                Events.ProcessingMessageGroups(routingMessages, routingMessageGroups.Count, this.cloudEndpoint.FanOutFactor);

                foreach (var groupBatch in routingMessageGroups.Batch(this.cloudEndpoint.FanOutFactor))
                {
                    IEnumerable<Task<ISinkResult<IRoutingMessage>>> sendTasks = groupBatch
                        .Select(item => this.ProcessClientMessages(item.Id, item.RoutingMessages, token));
                    ISinkResult<IRoutingMessage>[] sinkResults = await Task.WhenAll(sendTasks);

                    foreach (var res in sinkResults)
                    {
                        result.Merge(res);
                    }
                }

                return result;
            }

            // Process all messages for a particular client
            async Task<ISinkResult<IRoutingMessage>> ProcessClientMessages(string id, List<IRoutingMessage> routingMessages, CancellationToken token)
            {
                var result = new MergingSinkResult<IRoutingMessage>();

                // Find the maximum message size, and divide messages into largest batches
                // not exceeding max allowed IoTHub message size.
                long maxMessageSize = routingMessages.Select(r => r.Size()).Max();
                int batchSize = GetBatchSize(Math.Min(this.cloudEndpoint.maxBatchSize, routingMessages.Count), maxMessageSize);

                var iterator = routingMessages.Batch(batchSize).GetEnumerator();
                while (iterator.MoveNext())
                {
                    result.Merge(await this.ProcessClientMessagesBatch(id, iterator.Current.ToList(), token));
                    if (!result.IsSuccessful)
                        break;
                }

                // if failed earlier, fast-fail the rest
                while (iterator.MoveNext())
                {
                    result.AddFailed(iterator.Current);
                }

                return result;
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

                Try<ICloudProxy> cloudProxy = await this.cloudEndpoint.cloudProxyGetterFunc(id);
                if (cloudProxy.Success)
                {
                    var cp = cloudProxy.Value;
                    try
                    {
                            List<IMessage> messages = routingMessages
                                .Select(r => this.cloudEndpoint.messageConverter.ToMessage(r))
                                .ToList();

                            if (messages.Count == 1)
                            {
                                await cp.SendMessageAsync(messages[0]);
                            }
                            else
                            {
                                await cp.SendMessageBatchAsync(messages);
                            }

                            return new SinkResult<IRoutingMessage>(routingMessages);
                        }
                        catch (Exception ex)
                        {
                            return this.HandleException(ex, id, routingMessages);
                        }
                }
                else
                {
                    if (this.IsRetryable(cloudProxy.Exception) || !this.trackDeviceState)
                    {
                        return HandleNoConnection(id, routingMessages);
                    }
                    else
                    {
                        return this.HandleException(cloudProxy.Exception, id, routingMessages);
                    }
                }
            }

            ISinkResult HandleException(Exception ex, string id, List<IRoutingMessage> routingMessages)
            {
                if (this.IsRetryable(ex))
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

            static MetricTags GetTags(string id)
            {
                return new MetricTags("DeviceId", id);
            }
        }
    }
}
