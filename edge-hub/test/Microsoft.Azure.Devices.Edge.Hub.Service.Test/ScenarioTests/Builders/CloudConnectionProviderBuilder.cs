// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;

    public class CloudConnectionProviderBuilder
    {
        private IClientProvider clientProvider = new GenericClientProvider<AllGoodClientBuilder>();
        private bool closeOnIdle = true;
        private TimeSpan cloudConnectionIdleTimeout = TimeSpan.FromHours(1);
        private TimeSpan cloudOperationTimeout = TimeSpan.FromSeconds(20);

        public static CloudConnectionProviderBuilder Create() => new CloudConnectionProviderBuilder();

        public CloudConnectionProviderBuilder WithClientProvider(IClientProvider clientProvider)
        {
            this.clientProvider = clientProvider;
            return this;
        }

        public CloudConnectionProviderBuilder WithClientProvider<T>(Func<T, T> clientBuilderDecorator)
           where T : IClientBuilder, new()
        {
            this.clientProvider = new GenericClientProvider<T>().WithBuilder(clientBuilderDecorator);
            return this;
        }

        public CloudConnectionProviderBuilder WithCloseOnIdle(bool closeOnIdle)
        {
            this.closeOnIdle = closeOnIdle;
            return this;
        }

        public CloudConnectionProviderBuilder WithCloudConnectionIdleTimeout(TimeSpan timeout)
        {
            this.cloudConnectionIdleTimeout = timeout;
            return this;
        }

        public CloudConnectionProviderBuilder WithCloudOperationTimeout(TimeSpan timeout)
        {
            this.cloudOperationTimeout = timeout;
            return this;
        }

        public CloudConnectionProvider Build()
        {
            var messageConverterProvider = new MessageConverterProvider(
                                                    new Dictionary<Type, IMessageConverter>()
                                                    {
                                                        [typeof(Client.Message)] = new DeviceClientMessageConverter(),
                                                        [typeof(Twin)] = new TwinMessageConverter(),
                                                        [typeof(TwinCollection)] = new TwinCollectionMessageConverter()
                                                    });

            return new CloudConnectionProvider(
                 messageConverterProvider,
                 1,
                 this.clientProvider,
                 Option.None<UpstreamProtocol>(),
                 new SimpleTokenProvider(),
                 new AllFitDeviceScopeIdentitiesCache(),
                 new NullCredentialsCache(),
                 new ModuleIdentity(TestContext.IotHubName, TestContext.DeviceId, "$edgeHub"),
                 this.cloudConnectionIdleTimeout,
                 this.closeOnIdle,
                 this.cloudOperationTimeout,
                 Option.None<System.Net.IWebProxy>(),
                 new ProductInfoStore(new StoreProvider(new InMemoryDbStoreProvider()).GetEntityStore<string, string>("ProductInfo"), string.Empty));
        }

        private class SimpleTokenProvider : ITokenProvider
        {
            public Task<string> GetTokenAsync(Option<TimeSpan> ttl)
            {
                return Task.FromResult("testToken");
            }
        }
    }
}
