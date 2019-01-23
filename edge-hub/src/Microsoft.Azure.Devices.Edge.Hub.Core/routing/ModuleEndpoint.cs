// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using ISinkResult = Microsoft.Azure.Devices.Routing.Core.ISinkResult<Devices.Routing.Core.IMessage>;
    using Option = Microsoft.Azure.Devices.Edge.Util.Option;

    public class ModuleEndpoint : Endpoint
    {
        readonly IConnectionManager connectionManager;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;
        readonly string moduleId;

        public ModuleEndpoint(string id, string moduleId, string input, IConnectionManager connectionManager, Core.IMessageConverter<IRoutingMessage> messageConverter)
            : base(id)
        {
            this.Input = Preconditions.CheckNonWhiteSpace(input, nameof(input));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
        }

        public override string Type => this.GetType().Name;

        public string Input { get; }

        public override IProcessor CreateProcessor() => new ModuleMessageProcessor(this);

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
            // TODO - No-op
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.ModuleEndpoint;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleEndpoint>();

            enum EventIds
            {
                NoDeviceProxy = IdStart,
                ErrorSendingMessages,
                RetryingMessages,
                InvalidMessage,
                ProcessingMessages
            }

            public static void NoDeviceProxy(ModuleEndpoint moduleEndpoint)
            {
                Log.LogWarning((int)EventIds.NoDeviceProxy, Invariant($"Module {moduleEndpoint.moduleId} is not connected"));
            }

            public static void ErrorSendingMessages(ModuleEndpoint moduleEndpoint, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorSendingMessages, ex, Invariant($"Error sending messages to module {moduleEndpoint.moduleId}"));
            }

            public static void ProcessingMessages(ModuleEndpoint moduleEndpoint, ICollection<IRoutingMessage> routingMessages)
            {
                Log.LogDebug((int)EventIds.ProcessingMessages, Invariant($"Sending {routingMessages.Count} message(s) to module {moduleEndpoint.moduleId}."));
            }

            public static void NoMessagesSubscription(string moduleId)
            {
                Log.LogWarning((int)EventIds.NoDeviceProxy, Invariant($"No subscription for receiving messages found for {moduleId}"));
            }

            internal static void RetryingMessages(int count, string endpointId)
            {
                // TODO - Add more info to this log message
                Log.LogDebug((int)EventIds.RetryingMessages, Invariant($"Retrying {count} messages to {endpointId}."));
            }

            internal static void InvalidMessage(Exception ex)
            {
                // TODO - Add more info to this log message
                Log.LogWarning((int)EventIds.InvalidMessage, ex, Invariant($"Non retryable exception occurred while sending message."));
            }
        }

        class ModuleMessageProcessor : IProcessor
        {
            static readonly ISet<Type> RetryableExceptions = new HashSet<Type>
            {
                typeof(TimeoutException),
                typeof(IOException),
                typeof(EdgeHubIOException)
            };

            readonly ModuleEndpoint moduleEndpoint;
            Util.Option<IDeviceProxy> devicePoxy = Option.None<IDeviceProxy>();

            public ModuleMessageProcessor(ModuleEndpoint endpoint)
            {
                this.moduleEndpoint = Preconditions.CheckNotNull(endpoint);
            }

            public Endpoint Endpoint => this.moduleEndpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(this.IsTransientException);

            public Task<ISinkResult> ProcessAsync(IRoutingMessage routingMessage, CancellationToken token)
            {
                return this.ProcessAsync(new[] { Preconditions.CheckNotNull(routingMessage, nameof(routingMessage)) }, token);
            }

            public async Task<ISinkResult> ProcessAsync(ICollection<IRoutingMessage> routingMessages, CancellationToken token)
            {
                Preconditions.CheckNotNull(routingMessages, nameof(routingMessages));

                Events.ProcessingMessages(this.moduleEndpoint, routingMessages);
                Util.Option<IDeviceProxy> deviceProxy = this.GetDeviceProxy();

                var result = await deviceProxy.Match<Task<ISinkResult>>(
                    dp => this.ProcessAsync(routingMessages, dp, token),
                    () => this.ProcessNoConnection(routingMessages));

                return result;
            }

            public Task CloseAsync(CancellationToken token)
            {
                // TODO - No-op
                return TaskEx.Done;
            }

            static bool IsRetryable(Exception ex) => ex != null && RetryableExceptions.Contains(ex.GetType());

            Task<ISinkResult> ProcessNoConnection(ICollection<IRoutingMessage> routingMessages)
            {
                var failed = new List<IRoutingMessage>();
                failed.AddRange(routingMessages);
                var sendFailureDetails = new SendFailureDetails(FailureKind.None, new EdgeHubConnectionException($"Target module {this.moduleEndpoint.moduleId} is not connected"));
                return Task.FromResult((ISinkResult<IRoutingMessage>)new SinkResult<IRoutingMessage>(ImmutableList<IRoutingMessage>.Empty, failed, sendFailureDetails));
            }

            async Task<ISinkResult> ProcessAsync(ICollection<IRoutingMessage> routingMessages, IDeviceProxy dp, CancellationToken token)
            {
                // TODO - figure out if we can use cancellation token to cancel send
                var succeeded = new List<IRoutingMessage>();
                var failed = new List<IRoutingMessage>();
                var invalid = new List<InvalidDetails<IRoutingMessage>>();
                SendFailureDetails sendFailureDetails = null;
                foreach (IRoutingMessage routingMessage in routingMessages)
                {
                    IMessage message = this.moduleEndpoint.messageConverter.ToMessage(routingMessage);
                    try
                    {
                        await dp.SendMessageAsync(message, this.moduleEndpoint.Input);
                        succeeded.Add(routingMessage);
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

                        Events.ErrorSendingMessages(this.moduleEndpoint, ex);
                    }
                }

                if (failed.Count > 0)
                {
                    Events.RetryingMessages(failed.Count, this.moduleEndpoint.Id);
                    sendFailureDetails = new SendFailureDetails(FailureKind.Transient, new EdgeHubIOException($"Error sending message to module {this.moduleEndpoint.moduleId}"));
                }

                return new SinkResult<IRoutingMessage>(succeeded, failed, invalid, sendFailureDetails);
            }

            bool IsTransientException(Exception ex) => ex is EdgeHubConnectionException
                                                       || ex is EdgeHubIOException;

            Util.Option<IDeviceProxy> GetDeviceProxy()
            {
                this.devicePoxy = this.devicePoxy.Filter(d => d.IsActive)
                    .Map(d => Option.Some(d))
                    .GetOrElse(
                        () =>
                        {
                            Util.Option<IDeviceProxy> currentDeviceProxy = this.moduleEndpoint.connectionManager.GetDeviceConnection(this.moduleEndpoint.moduleId).Filter(d => d.IsActive);
                            if (currentDeviceProxy.HasValue)
                            {
                                if (this.moduleEndpoint.connectionManager.GetSubscriptions(this.moduleEndpoint.moduleId)
                                    .Filter(s => s.TryGetValue(DeviceSubscription.ModuleMessages, out bool isActive) && isActive)
                                    .HasValue)
                                {
                                    return currentDeviceProxy;
                                }
                                else
                                {
                                    Events.NoMessagesSubscription(this.moduleEndpoint.moduleId);
                                }
                            }
                            else
                            {
                                Events.NoDeviceProxy(this.moduleEndpoint);
                            }

                            return Option.None<IDeviceProxy>();
                        });

                return this.devicePoxy;
            }
        }
    }
}
