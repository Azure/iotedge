// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Util;
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

        public ModuleEndpoint(string id, string address, Func<Util.Option<IDeviceProxy>> deviceProxyGetterFunc, Core.IMessageConverter<IRoutingMessage> messageConverter)
            : base(id)
        {
            this.EndpointAddress = Preconditions.CheckNotNull(address);
            this.deviceProxyGetterFunc = Preconditions.CheckNotNull(deviceProxyGetterFunc);
            this.messageConverter = Preconditions.CheckNotNull(messageConverter);
        }

        public override string Type => this.GetType().Name;

        public override IProcessor CreateProcessor() => new ModuleMessageProcessor(this);

        public string EndpointAddress { get; }

        public override void LogUserMetrics(long messageCount, long latencyInMs)
        {
            // TODO - No-op
        }

        class ModuleMessageProcessor : IProcessor
        {
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
                SendFailureDetails sendFailureDetails = null;

                await this.GetDeviceProxy()
                    .Match(
                        async (c) =>
                        {
                            foreach (IRoutingMessage routingMessage in routingMessages)
                            {
                                IMessage message = this.moduleEndpoint.messageConverter.ToMessage(routingMessage);
                                bool res = await c.SendMessage(message, this.moduleEndpoint.EndpointAddress);
                                if (res)
                                {
                                    succeeded.Add(routingMessage);
                                }
                                else
                                {
                                    failed.Add(routingMessage);
                                }
                            }
                        },
                        () =>
                        {
                            // TODO - Check if this should be failed instead. 
                            sendFailureDetails = new SendFailureDetails(FailureKind.InternalError, new EdgeHubConnectionException("No connection to IoTHub found"));
                            return TaskEx.Done;
                        });

                return new SinkResult<IRoutingMessage>(succeeded, failed, sendFailureDetails);
            }

            public Task CloseAsync(CancellationToken token)
            {
                // TODO - No-op
                return TaskEx.Done;
            }

            public Endpoint Endpoint => this.moduleEndpoint;

            public ITransientErrorDetectionStrategy ErrorDetectionStrategy => new ErrorDetectionStrategy(_ => false);

            Util.Option<IDeviceProxy> GetDeviceProxy()
            {
                this.devicePoxy = this.devicePoxy.Filter(d => d.IsActive).Match(
                    d => Option.Some(d),
                    () => this.moduleEndpoint.deviceProxyGetterFunc());
                return this.devicePoxy;
            }
        }
    }
}