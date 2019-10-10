// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EndpointBuilder
    {
        private string id = "test-endpoint-" + NextEndpointNumber();
        private Func<string, Task<Option<ICloudProxy>>> cloudProxyGetterFunc = _ => Task.FromResult(Option.Some(new AllGoodCloudProxy() as ICloudProxy));
        private Core.IMessageConverter<Routing.Core.IMessage> messageConverter = new Core.Routing.RoutingMessageConverter();
        private int maxBatchSize = 10;
        private int fanoutFactor = 10;

        private bool asModuleEndpoint = false;

        static int endpointCounter;
        static int NextEndpointNumber() => Interlocked.Increment(ref endpointCounter);

        public static EndpointBuilder Create() => new EndpointBuilder();

        public EndpointBuilder AsCloudEndpoint()
        {
            this.asModuleEndpoint = false;
            return this;
        }

        public EndpointBuilder AsModuleEndpoint()
        {
            this.asModuleEndpoint = true;
            return this;
        }

        public EndpointBuilder WithId(string id)
        {
            this.id = id;
            return this;
        }

        public EndpointBuilder WithProxy<T>()
            where T : ICloudProxy, new()
        {
            this.cloudProxyGetterFunc = _ => Task.FromResult(Option.Some(new T() as ICloudProxy));
            return this;
        }

        public EndpointBuilder WithProxyGetter(Func<string, Task<Option<ICloudProxy>>> getter)
        {
            this.cloudProxyGetterFunc = getter;
            return this;
        }

        public EndpointBuilder WithMessageConverter<T>()
            where T : Core.IMessageConverter<Routing.Core.IMessage>, new()
        {
            this.messageConverter = new T();
            return this;
        }

        public EndpointBuilder WithMessageConverter(Core.IMessageConverter<Routing.Core.IMessage> messageConverter)
        {
            this.messageConverter = messageConverter;
            return this;
        }

        public EndpointBuilder WithBatchSize(int batchSize)
        {
            this.maxBatchSize = batchSize;
            return this;
        }

        public EndpointBuilder WithFanoutFactor(int fanoutFactor)
        {
            this.fanoutFactor = fanoutFactor;
            return this;
        }

        public Routing.Core.Endpoint Build()
        {
            if (this.asModuleEndpoint)
            {
                throw new NotImplementedException("Sorry, you need to add the logic to EndpointBuiler class to build a ModuleEndpoint");
            }

            var result = new Core.Routing.CloudEndpoint(
                                this.id,
                                this.cloudProxyGetterFunc,
                                this.messageConverter,
                                this.maxBatchSize,
                                this.fanoutFactor);

            return result;
        }
    }
}
