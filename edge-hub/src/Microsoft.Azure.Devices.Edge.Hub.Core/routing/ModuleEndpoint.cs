// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using System.Collections.Generic;
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
    using Endpoint = Microsoft.Azure.Devices.Routing.Core.Endpoint;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IProcessor = Microsoft.Azure.Devices.Routing.Core.IProcessor;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using ISinkResult = Microsoft.Azure.Devices.Routing.Core.ISinkResult<Microsoft.Azure.Devices.Routing.Core.IMessage>;
    using Option = Microsoft.Azure.Devices.Edge.Util.Option;
    using TaskEx = Microsoft.Azure.Devices.Edge.Util.TaskEx;

    public class ModuleEndpoint : Endpoint
    {
        readonly Func<Util.Option<IDeviceProxy>> deviceProxyGetterFunc;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;
        readonly string moduleId;

        public ModuleEndpoint(string id, string moduleId, string input, Func<Util.Option<IDeviceProxy>> deviceProxyGetterFunc, Core.IMessageConverter<IRoutingMessage> messageConverter)
            : base(id)
        {
            this.Input = Preconditions.CheckNotNull(input);
            this.deviceProxyGetterFunc = Preconditions.CheckNotNull(deviceProxyGetterFunc);
            this.messageConverter = Preconditions.CheckNotNull(messageConverter);
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
        }

        public override string Type => this.GetType().Name;

        public override IProcessor CreateProcessor() => new ModuleMessageProcessor(this);

        public string Input { get; }

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
            // TODO - No-op
        }

        class ModuleMessageProcessor : IProcessor
        {
            static readonly ISet<Type> RetryableExceptions = new HashSet<Type>
            {
                typeof(TimeoutException),
                typeof(IOException),
                typeof(EdgeHubIOException)
            };

            Util.Option<IDeviceProxy> devicePoxy = Option.None<IDeviceProxy>();
            readonly ModuleEndpoint moduleEndpoint;

            public ModuleMessageProcessor(ModuleEndpoint endpoint)
            {
                this.moduleEndpoint = Preconditions.CheckNotNull(endpoint);
            }

            public Task<ISinkResult> ProcessAsync(IRoutingMessage routingMessage, CancellationToken token)
            {
                return this.ProcessAsync(new[] { Preconditions.CheckNotNull(routingMessage, nameof(routingMessage)) }, token);
            }

            public async Task<ISinkResult> ProcessAsync(ICollection<IRoutingMessage> routingMessages, CancellationToken token)
            {
                Preconditions.CheckNotNull(routingMessages, nameof(routingMessages));

                // TODO - figure out if we can use cancellation token to cancel send
                var succeeded = new List<IRoutingMessage>();
                var failed = new List<IRoutingMessage>();
                var invalid = new List<InvalidDetails<IRoutingMessage>>();
                SendFailureDetails sendFailureDetails = null;

                Util.Option<IDeviceProxy> deviceProxy = this.GetDeviceProxy();

                if (!deviceProxy.HasValue)
                {
                    failed.AddRange(routingMessages);
                    sendFailureDetails = new SendFailureDetails(FailureKind.None, new EdgeHubConnectionException($"Target module {this.moduleEndpoint.moduleId} is not connected"));
                    Events.NoDeviceProxy(this.moduleEndpoint);
                }
                else
                {
                    foreach (IRoutingMessage routingMessage in routingMessages)
                    {
                        IMessage message = this.moduleEndpoint.messageConverter.ToMessage(routingMessage);
                        await deviceProxy.ForEachAsync(async dp =>
                        {
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
                        });
                    }

                    if (failed.Count > 0)
                    {
                        Events.RetryingMessages(failed.Count, this.moduleEndpoint.Id);
                        sendFailureDetails = new SendFailureDetails(FailureKind.Transient, new EdgeHubIOException($"Error sending message to module {this.moduleEndpoint.moduleId}"));
                    }
                }

                return new SinkResult<IRoutingMessage>(succeeded, failed, sendFailureDetails);
            }

            public Task CloseAsync(CancellationToken token)
            {
                // TODO - No-op
                return TaskEx.Done;
            }

            public Endpoint Endpoint => this.moduleEndpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(this.IsTransientException);

            bool IsTransientException(Exception ex) => ex is EdgeHubConnectionException
                || ex is EdgeHubIOException;

            Util.Option<IDeviceProxy> GetDeviceProxy()
            {
                this.devicePoxy = this.devicePoxy.Filter(d => d.IsActive).Match(
                    d => Option.Some(d),
                    () => this.moduleEndpoint.deviceProxyGetterFunc().Filter(d => d.IsActive));
                return this.devicePoxy;
            }

            static bool IsRetryable(Exception ex) => ex != null && RetryableExceptions.Contains(ex.GetType());
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleEndpoint>();
            const int IdStart = HubCoreEventIds.ModuleEndpoint;

            enum EventIds
            {
                NoDeviceProxy = IdStart,
                ErrorSendingMessages,
                RetryingMessages,
                InvalidMessage
            }

            public static void NoDeviceProxy(ModuleEndpoint moduleEndpoint)
            {
                Log.LogError((int)EventIds.NoDeviceProxy, Invariant($"Module {moduleEndpoint.moduleId} is not connected"));
            }

            public static void ErrorSendingMessages(ModuleEndpoint moduleEndpoint, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorSendingMessages, ex, Invariant($"Error sending messages to module {moduleEndpoint.moduleId}"));
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
    }
}
