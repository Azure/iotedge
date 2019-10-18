// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EndpointBuilder
    {
        private string id = "test-endpoint-" + NextEndpointNumber();
        private string moduleId = TestContext.ModuleId;
        private string input = "test-input";

        private ConnectionManagerBuilder connectionManagerBuilder = ConnectionManagerBuilder.Create();
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

        public EndpointBuilder WithModuleId(string moduleId)
        {
            this.moduleId = moduleId;
            return this;
        }

        public EndpointBuilder WithInput(string input)
        {
            this.input = input;
            return this;
        }

        public EndpointBuilder WithModuleProxy(IDeviceProxy deviceProxy)
        {
            this.AsModuleEndpoint();
            this.connectionManagerBuilder
                .WithConnectedDevice(
                    device => device.AsModule().WithDeviceProxy(deviceProxy));

            return this;
        }

        public EndpointBuilder WithModuleProxy<T>()
            where T : IDeviceProxy, new()
        {
            this.AsModuleEndpoint();
            this.connectionManagerBuilder
                .WithConnectedDevice(device => device.AsModule().WithDeviceProxy<T>());

            return this;
        }

        public EndpointBuilder WithCloudProxy<T>()
            where T : ICloudProxy, new()
        {
            this.AsCloudEndpoint();
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

        public EndpointBuilder WithConnectionManager(Func<ConnectionManagerBuilder, ConnectionManagerBuilder> connectionManager)
        {
            connectionManager(this.connectionManagerBuilder);
            return this;
        }

        public Routing.Core.Endpoint Build()
        {
            var result = default(Routing.Core.Endpoint);

            if (this.asModuleEndpoint)
            {
                result = new Core.Routing.ModuleEndpoint(
                                    this.id,
                                    this.moduleId,
                                    this.input,
                                    this.connectionManagerBuilder.Build(),
                                    this.messageConverter);
            }
            else
            {
                result = new Core.Routing.CloudEndpoint(
                                    this.id,
                                    this.cloudProxyGetterFunc,
                                    this.messageConverter,
                                    this.maxBatchSize,
                                    this.fanoutFactor);
            }

            return result;
        }
    }
}
