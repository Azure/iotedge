// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;
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
        readonly Func<string, Util.Option<ICloudProxy>> cloudProxyGetterFunc;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;

        public CloudEndpoint(string id, Func<string, Util.Option<ICloudProxy>> cloudProxyGetterFunc, Core.IMessageConverter<IRoutingMessage> messageConverter)
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
                typeof(IotHubException)
            };

            readonly CloudEndpoint cloudEndpoint;

            public CloudMessageProcessor(CloudEndpoint endpoint)
            {
                this.cloudEndpoint = Preconditions.CheckNotNull(endpoint);
            }

            public async Task<ISinkResult> ProcessAsync(IRoutingMessage routingMessage, CancellationToken token)
            {
                var succeeded = new List<IRoutingMessage>();
                var failed = new List<IRoutingMessage>();
                var invalid = new List<InvalidDetails<IRoutingMessage>>();
                SendFailureDetails sendFailureDetails = null;

                IMessage message = this.cloudEndpoint.messageConverter.ToMessage(Preconditions.CheckNotNull(routingMessage, nameof(routingMessage)));
                await this.GetCloudProxy(routingMessage)
                    .Match(
                        async (c) =>
                        {
                            try
                            {
                                await c.SendMessageAsync(message);
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
                                    invalid.Add(new InvalidDetails<IRoutingMessage>(routingMessage, FailureKind.None));
                                }

                                if (failed.Count > 0)
                                {
                                    sendFailureDetails = new SendFailureDetails(FailureKind.Transient, new EdgeHubIOException($"Error sending messages to IotHub for device {this.cloudEndpoint.Id}"));
                                }
                            }
                        },
                        () =>
                        {
                            sendFailureDetails = new SendFailureDetails(FailureKind.None, new EdgeHubConnectionException("IoTHub is not connected"));
                            failed.Add(routingMessage);
                            Events.IoTHubNotConnected(this.Endpoint.Id);
                            return TaskEx.Done;
                        });

                return new SinkResult<IRoutingMessage>(succeeded, failed, invalid, sendFailureDetails);
            }

            public async Task<ISinkResult> ProcessAsync(ICollection<IRoutingMessage> routingMessages, CancellationToken token)
            {
                var succeeded = new List<IRoutingMessage>();
                var failed = new List<IRoutingMessage>();
                var invalid = new List<InvalidDetails<IRoutingMessage>>();

                foreach (IRoutingMessage routingMessage in Preconditions.CheckNotNull(routingMessages, nameof(routingMessages)))
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    ISinkResult res = await this.ProcessAsync(routingMessage, token);
                    succeeded.AddRange(res.Succeeded);
                    failed.AddRange(res.Failed);
                    invalid.AddRange(res.InvalidDetailsList);
                }

                return new SinkResult<IRoutingMessage>(succeeded, failed, invalid, null);
            }

            public Task CloseAsync(CancellationToken token)
            {
                // TODO - No-op
                return TaskEx.Done;
            }

            public Endpoint Endpoint => this.cloudEndpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(this.IsTransientException);

            bool IsTransientException(Exception ex) => ex is EdgeHubConnectionException;

            Util.Option<ICloudProxy> GetCloudProxy(IRoutingMessage routingMessage)
            {
                if (routingMessage.SystemProperties.TryGetValue(SystemProperties.ConnectionDeviceId, out string deviceId))
                {
                    string id = routingMessage.SystemProperties.TryGetValue(SystemProperties.ConnectionModuleId, out string moduleId)
                        ? $"{deviceId}/{moduleId}"
                        : deviceId;
                    Util.Option<ICloudProxy> cloudProxy = this.cloudEndpoint.cloudProxyGetterFunc(id)
                        .Filter(c => c.IsActive);
                    return cloudProxy;
                }
                Events.DeviceIdNotFound(routingMessage);
                return Option.None<ICloudProxy>();
            }

            static bool IsRetryable(Exception ex) => ex != null && RetryableExceptions.Contains(ex.GetType());
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudEndpoint>();
            const int IdStart = HubCoreEventIds.CloudEndpoint;

            enum EventIds
            {
                DeviceIdNotFound = IdStart,
                IoTHubNotConnected
            }

            public static void DeviceIdNotFound(IRoutingMessage routingMessage)
            {
                string message = routingMessage.SystemProperties.TryGetValue(SystemProperties.MessageId, out string messageId)
                    ? Invariant($"Message with MessageId {messageId} does not contain a device Id.")
                    : "Received message does not contain a device Id";
                Log.LogError((int)EventIds.DeviceIdNotFound, message);
            }

            internal static void IoTHubNotConnected(string id)
            {
                Log.LogWarning((int)EventIds.IoTHubNotConnected, Invariant($"Could not get an active IotHub connection for device {id}"));
            }
        }
    }
}