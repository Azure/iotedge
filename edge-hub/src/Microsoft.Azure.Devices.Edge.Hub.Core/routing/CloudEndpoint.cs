// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
    using Endpoint = Microsoft.Azure.Devices.Routing.Core.Endpoint;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IProcessor = Microsoft.Azure.Devices.Routing.Core.IProcessor;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using ISinkResult = Microsoft.Azure.Devices.Routing.Core.ISinkResult<Microsoft.Azure.Devices.Routing.Core.IMessage>;
    using Option = Microsoft.Azure.Devices.Edge.Util.Option;
    using SystemProperties = Microsoft.Azure.Devices.Edge.Hub.Core.SystemProperties;

    public class CloudEndpoint : Endpoint
    {
        readonly Func<string, Task<Util.Option<ICloudProxy>>> cloudProxyGetterFunc;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;

        public CloudEndpoint(string id, Func<string, Task<Util.Option<ICloudProxy>>> cloudProxyGetterFunc, Core.IMessageConverter<IRoutingMessage> messageConverter)
            : base(id)
        {
            this.cloudProxyGetterFunc = Preconditions.CheckNotNull(cloudProxyGetterFunc);
            this.messageConverter = Preconditions.CheckNotNull(messageConverter);
        }

        public override string Type => this.GetType().Name;

        public override IProcessor CreateProcessor() => new CloudMessageProcessor(this);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
            // TODO - No-op for now
        }

        class CloudMessageProcessor : IProcessor
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

            public async Task<ISinkResult> ProcessAsync(IRoutingMessage routingMessage, CancellationToken token)
            {
                Preconditions.CheckNotNull(routingMessage, nameof(routingMessage));
                var succeeded = new List<IRoutingMessage>();
                var failed = new List<IRoutingMessage>();
                var invalid = new List<InvalidDetails<IRoutingMessage>>();
                SendFailureDetails sendFailureDetails = null;

                IMessage message = this.cloudEndpoint.messageConverter.ToMessage(routingMessage);

                Util.Option<string> identity = this.GetIdentity(routingMessage);
                if (!identity.HasValue)
                {
                    Events.InvalidMessageNoIdentity();
                    invalid.Add(new InvalidDetails<IRoutingMessage>(routingMessage, FailureKind.None));
                }
                else
                {
                    await identity.ForEachAsync(
                        async id =>
                        {
                            Util.Option<ICloudProxy> cloudProxy = await this.cloudEndpoint.cloudProxyGetterFunc(id);
                            if (!cloudProxy.HasValue)
                            {
                                Events.IoTHubNotConnected(id);
                                sendFailureDetails = new SendFailureDetails(FailureKind.Transient, new EdgeHubConnectionException("IoT Hub is not connected"));
                                failed.Add(routingMessage);
                            }
                            else
                            {
                                await cloudProxy.ForEachAsync(async cp =>
                                {
                                    try
                                    {
                                        using (Metrics.CloudLatency(id))
                                        {
                                            await cp.SendMessageAsync(message);
                                        }
                                        succeeded.Add(routingMessage);
                                        Metrics.MessageCount(id);
                                    }
                                    catch (Exception ex)
                                    {
                                        if (IsRetryable(ex))
                                        {
                                            failed.Add(routingMessage);
                                        }
                                        else
                                        {
                                            Events.InvalidMessage(ex);
                                            invalid.Add(new InvalidDetails<IRoutingMessage>(routingMessage, FailureKind.None));
                                        }

                                        if (failed.Count > 0)
                                        {
                                            Events.RetryingMessage(routingMessage, ex);
                                            sendFailureDetails = new SendFailureDetails(FailureKind.Transient, new EdgeHubIOException($"Error sending messages to IotHub for device {this.cloudEndpoint.Id}"));
                                        }
                                    }
                                });
                            }
                        });
                }

                return new SinkResult<IRoutingMessage>(succeeded, failed, invalid, sendFailureDetails);
            }

            public async Task<ISinkResult> ProcessAsync(ICollection<IRoutingMessage> routingMessages, CancellationToken token)
            {
                Preconditions.CheckNotNull(routingMessages, nameof(routingMessages));
                var succeeded = new List<IRoutingMessage>();
                var failed = new List<IRoutingMessage>();
                var invalid = new List<InvalidDetails<IRoutingMessage>>();
                Devices.Routing.Core.Util.Option<SendFailureDetails> sendFailureDetails =
                    Devices.Routing.Core.Util.Option.None<SendFailureDetails>();

                Events.ProcessingMessages(routingMessages);
                foreach (IRoutingMessage routingMessage in routingMessages)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    ISinkResult res = await this.ProcessAsync(routingMessage, token);
                    succeeded.AddRange(res.Succeeded);
                    failed.AddRange(res.Failed);
                    invalid.AddRange(res.InvalidDetailsList);
                    sendFailureDetails = res.SendFailureDetails;
                }

                return new SinkResult<IRoutingMessage>(succeeded, failed, invalid,
                    sendFailureDetails.GetOrElse(null));
            }

            public Task CloseAsync(CancellationToken token)
            {
                // TODO - No-op
                return TaskEx.Done;
            }

            public Endpoint Endpoint => this.cloudEndpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(this.IsTransientException);

            bool IsTransientException(Exception ex) => ex is EdgeHubIOException || ex is EdgeHubConnectionException;

            Util.Option<string> GetIdentity(IRoutingMessage routingMessage)
            {
                if (routingMessage.SystemProperties.TryGetValue(SystemProperties.ConnectionDeviceId, out string deviceId))
                {
                    return Option.Some(routingMessage.SystemProperties.TryGetValue(SystemProperties.ConnectionModuleId, out string moduleId)
                        ? $"{deviceId}/{moduleId}"
                        : deviceId);
                }
                Events.DeviceIdNotFound(routingMessage);
                return Option.None<string>();
            }

            static bool IsRetryable(Exception ex) => ex != null && RetryableExceptions.Contains(ex.GetType());
        }

        static class Metrics
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

            public static void MessageCount(string identity) => Util.Metrics.CountIncrement(GetTags(identity), EdgeHubToCloudMessageCountOptions, 1);

            public static IDisposable CloudLatency(string identity) => Util.Metrics.Latency(GetTags(identity), EdgeHubToCloudMessageLatencyOptions);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudEndpoint>();
            const int IdStart = HubCoreEventIds.CloudEndpoint;

            enum EventIds
            {
                DeviceIdNotFound = IdStart,
                IoTHubNotConnected,
                RetryingMessages,
                InvalidMessage,
                ProcessingMessages,
                InvalidMessageNoIdentity
            }

            public static void DeviceIdNotFound(IRoutingMessage routingMessage)
            {
                string message = routingMessage.SystemProperties.TryGetValue(SystemProperties.MessageId, out string messageId)
                    ? Invariant($"Message with MessageId {messageId} does not contain a device Id.")
                    : "Received message does not contain a device Id";
                Log.LogWarning((int)EventIds.DeviceIdNotFound, message);
            }

            internal static void IoTHubNotConnected(string id)
            {
                Log.LogWarning((int)EventIds.IoTHubNotConnected, Invariant($"Could not get an active Iot Hub connection for device {id}"));
            }

            internal static void RetryingMessage(IRoutingMessage message, Exception ex)
            {
                if (message.SystemProperties.TryGetValue(SystemProperties.ConnectionDeviceId, out string deviceId))
                {
                    string id = message.SystemProperties.TryGetValue(SystemProperties.ConnectionModuleId, out string moduleId)
                        ? $"{deviceId}/{moduleId}"
                        : deviceId;

                    // TODO - Add more info to this log message
                    Log.LogDebug((int)EventIds.RetryingMessages, Invariant($"Retrying sending message from {id} to Iot Hub due to exception {ex.GetType()}:{ex.Message}."));
                }
                else
                {
                    Log.LogDebug((int)EventIds.RetryingMessages, Invariant($"Retrying sending message to Iot Hub due to exception {ex.GetType()}:{ex.Message}."));
                }
            }

            internal static void InvalidMessage(Exception ex)
            {
                // TODO - Add more info to this log message
                Log.LogWarning((int)EventIds.InvalidMessage, ex, Invariant($"Non retryable exception occurred while sending message."));
            }

            public static void ProcessingMessages(ICollection<IRoutingMessage> routingMessages)
            {
                Log.LogDebug((int)EventIds.ProcessingMessages, Invariant($"Sending {routingMessages.Count} message(s) upstream."));
            }

            public static void InvalidMessageNoIdentity()
            {
                Log.LogWarning((int)EventIds.InvalidMessageNoIdentity, "Cannot process message with no identity, discarding it.");
            }
        }
    }
}
