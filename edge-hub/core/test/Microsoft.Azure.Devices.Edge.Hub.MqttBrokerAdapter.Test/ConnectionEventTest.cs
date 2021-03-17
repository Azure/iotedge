// Copyright (c) Microsoft. All rights reserved.namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Bson;
    using Newtonsoft.Json.Serialization;
    using Moq;
    using Xunit;

    [Integration]
    public class ConnectionEventTest
    {
        const int SafeProcessingDelayMs = 2000;

        static readonly TimeSpan TotalProcessingTimeout = TimeSpan.FromSeconds(30);

        static readonly string iotHubName = "testHub";
        static readonly string edgeModuleName = "$edgeHub";
        static readonly string edgeDeviceId = "testDevice";

        [Fact]
        public async Task OnReconnectionClientsGetResubscribed()
        {
            // The logic of this test is the following
            //  - First, generate the environment (edgeHub) with the necessary dependencies, so it can be emulated that
            //    the MQTT broker sends connection-change-notifications, and on reconnect, edgeHub can execute its
            //    resubscription cycle.
            //  - The way we validate that edgeHub reacts to the connection notifications is that we catch the resubscriptions
            //    on reconnect.
            //  - The testcode generates clients (both module and device, and direct and nested), all clients subscribe to
            //    desired property change, so after reconnection, edgeHub should re-subscribe for all clients.
            var (subscriptionChangeHandler, cloudProxyDispatcher, brokerConnector) = await SetupEnvironment();
            var allClients = await ConnectClientsAsync(subscriptionChangeHandler);
            var clients = new HashSet<IIdentity>();

            var lockObject = new object();

            await SetupSpyAndGenerateEvents(Spy, cloudProxyDispatcher, brokerConnector);            
            await WaitTillClientCountMatch(allClients, clients, lockObject, TotalProcessingTimeout);

            Assert.True(allClients.SetEquals(clients));

            void Spy(RpcPacket packet)
            {
                if (packet.Topic.EndsWith("/methods/post/#"))
                {
                    var segments = packet.Topic.Split('/');

                    lock (lockObject)
                    {
                        if (segments.Length == 6)
                        {
                            clients.Add(new ModuleIdentity(iotHubName, segments[1], segments[2]));
                        }
                        else if (segments.Length == 5)
                        {
                            clients.Add(new DeviceIdentity(iotHubName, segments[1]));
                        }
                    }
                }
            }
        }

        [Fact]
        public async Task OnReconnectionClientsGetTwinsPulled()
        {
            // This test is similar to OnReconnectionClientsGetResubscribed(), except it check if on reconnection
            // the device/module twins are pulled for every client
            var (subscriptionChangeHandler, cloudProxyDispatcher, brokerConnector) = await SetupEnvironment();
            var allClients = await ConnectClientsAsync(subscriptionChangeHandler);
            var clients = new HashSet<IIdentity>();

            var lockObject = new object();

            await SetupSpyAndGenerateEvents(Spy, cloudProxyDispatcher, brokerConnector);
            await WaitTillClientCountMatch(allClients, clients, lockObject, TotalProcessingTimeout);

            Assert.True(allClients.SetEquals(clients));

            void Spy(RpcPacket packet)
            {
                const string TwinGetPublishPattern = @"^((\$edgehub)|(\$iothub))/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/get/\?\$rid=(?<rid>.+)";

                var match = Regex.Match(packet.Topic, TwinGetPublishPattern);
                if (match.Success)
                {
                    var id1 = match.Groups["id1"];
                    var id2 = match.Groups["id2"];
                    var rid = match.Groups["rid"];
                    
                    var identity = id2.Success
                                        ? new ModuleIdentity(iotHubName, id1.Value, id2.Value)
                                        : new DeviceIdentity(iotHubName, id1.Value) as IIdentity;

                    _ = cloudProxyDispatcher.HandleAsync(
                            new MqttPublishInfo(
                                   $"$downstream/{identity.Id}/twin/res/200/?$rid={rid.Value}",
                                   Encoding.UTF8.GetBytes(@"{""deviceId"":null,""etag"":null,""version"":null,""properties"":{""desired"":{""$version"":1},""reported"":{""$version"":1}}}")));
                    
                    clients.Add(identity);
                }
            }
        }

        [Fact]
        public async Task WhenNoConnectionMessagesDontGetDropped()
        {
            // The motivation of this test was a bug, when throwing a bad exception type caused edgeHub to drop messages
            // while edgeHubCore was disconnected from the MQTT broker. This test ensures that the message drop was
            // due to the wrong exception type, and now that it is fixed, no message drop occures.
            var (subscriptionChangeHandler, cloudProxyDispatcher, brokerConnector) = await SetupEnvironment();
            var milestone = new SemaphoreSlim(0, 1);
            var shouldReceiveNow = false;
            var deviceId = "device_1";
            var messageContent = "test message";

            var edgeHub = cloudProxyDispatcher.AsPrivateAccessible().edgeHub as IEdgeHub;
            brokerConnector.SetPacketSpy(Spy);

            var identity = new DeviceIdentity(iotHubName, deviceId);
            await edgeHub.ProcessDeviceMessage(identity, new EdgeMessage(Encoding.UTF8.GetBytes(messageContent), new Dictionary<string, string>(), new Dictionary<string, string>() { [Core.SystemProperties.ConnectionDeviceId] = deviceId } ));

            // the bridge-connector keeps trying for 5 seconds, so let's wait a safe 10 seconds to be sure that the first attempt to send a message fails
            await Task.Delay(TimeSpan.FromSeconds(10));

            shouldReceiveNow = true;
            await cloudProxyDispatcher.HandleAsync(new MqttPublishInfo("$internal/connectivity", Encoding.UTF8.GetBytes("{\"status\":\"Connected\"}")));

            // the retry-config is to retry every 5 sec, so 10 should be enough
            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(10)));

            void Spy(RpcPacket packet)
            {
                Assert.True(shouldReceiveNow);
                Assert.Equal("pub", packet.Cmd);
                Assert.Equal(messageContent, Encoding.UTF8.GetString(packet.Payload));

                milestone.Release();
            }
        }

        async Task SetupSpyAndGenerateEvents(Action<RpcPacket> spy, IMessageConsumer cloudProxyDispatcher, NullBrokerConnector brokerConnector)
        {
            await cloudProxyDispatcher.HandleAsync(new MqttPublishInfo("$internal/connectivity", Encoding.UTF8.GetBytes("{\"status\":\"Connected\"}")));
            await Task.Delay(SafeProcessingDelayMs);

            await cloudProxyDispatcher.HandleAsync(new MqttPublishInfo("$internal/connectivity", Encoding.UTF8.GetBytes("{\"status\":\"Disconnected\"}")));
            await Task.Delay(SafeProcessingDelayMs);

            brokerConnector.SetPacketSpy(spy);

            await cloudProxyDispatcher.HandleAsync(new MqttPublishInfo("$internal/connectivity", Encoding.UTF8.GetBytes("{\"status\":\"Connected\"}")));
        }

        async Task WaitTillClientCountMatch(HashSet<IIdentity> clientSet1, HashSet<IIdentity> clientSet2, object lockObject, TimeSpan timeout)
        {
            // We don't know when all the clients get processed, so keep looping for a while
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < timeout)
            {
                bool isCountEqual;
                lock (lockObject)
                {
                    isCountEqual = clientSet1.Count == clientSet2.Count;
                }

                if (isCountEqual)
                {
                    break;
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }

        async Task<HashSet<IIdentity>> ConnectClientsAsync(IMessageConsumer subscriptionChangeHandler)
        {
            var rnd = new Random(921752);

            var allClients = new HashSet<IIdentity>();
            var indirectClients = new List<IIdentity>();
            for (var i = 0; i < 100; i++)
            {
                bool isModule = 0.5 > rnd.NextDouble();
                bool isDirect = 0.5 > rnd.NextDouble();

                var identity = isModule ? new ModuleIdentity(iotHubName, "device_" + i.ToString(), "module_" + i.ToString()) as IIdentity
                                        : new DeviceIdentity(iotHubName, "device_" + i.ToString()) as IIdentity;

                allClients.Add(identity);

                if (isDirect)
                {
                    await subscriptionChangeHandler.HandleAsync(new MqttPublishInfo($"$edgehub/{identity.Id}/subscriptions", Encoding.UTF8.GetBytes($"[\"$edgehub/{identity.Id}/methods/post/#\"]")));
                }
                else
                {
                    // for nested clients the subscriptions go together, because all sent by $edgeHub
                    indirectClients.Add(identity);
                }
            }

            var subscription = "[" + indirectClients.Select(c => $"\"$iothub/{c.Id}/methods/post/#\"").Join(", ") + "]";
            var topic = "$edgehub/nested_dev/$edgeHub/subscriptions";

            await subscriptionChangeHandler.HandleAsync(new MqttPublishInfo(topic, Encoding.UTF8.GetBytes(subscription)));

            return allClients;
        }

        async Task<(IMessageConsumer, IMessageConsumer, NullBrokerConnector)> SetupEnvironment()
        {
            Routing.UserMetricLogger = NullRoutingUserMetricLogger.Instance;
            Routing.PerfCounter = NullRoutingPerfCounter.Instance;
            Routing.UserAnalyticsLogger = NullUserAnalyticsLogger.Instance;

            var defaultRetryStrategy = new FixedInterval(5, TimeSpan.FromSeconds(5));
            var defaultRevivePeriod = TimeSpan.FromHours(1);
            var defaultTimeout = TimeSpan.FromSeconds(60);
            var endpointExecutorConfig = new EndpointExecutorConfig(defaultTimeout, defaultRetryStrategy, defaultRevivePeriod, true);

            var cloudProxyDispatcher = new BrokeredCloudProxyDispatcher();
            var cloudConnectionProvider = new BrokeredCloudConnectionProvider(cloudProxyDispatcher, new NullDeviceScopeIdentitiesCache());

            var identityProvider = new IdentityProvider(iotHubName);
            var deviceConnectivityManager = new BrokeredDeviceConnectivityManager(cloudProxyDispatcher);

            var connectionManager = new ConnectionManager(cloudConnectionProvider, Mock.Of<ICredentialsCache>(), new IdentityProvider(iotHubName), deviceConnectivityManager);

            var routingMessageConverter = new RoutingMessageConverter();
            var routeFactory = new EdgeRouteFactory(new EndpointFactory(connectionManager, routingMessageConverter, edgeDeviceId, 10, 10, true));
            var routesList = new[] { routeFactory.Create("FROM /messages INTO $upstream") };
            var endpoints = routesList.Select(r => r.Endpoint);
            var routerConfig = new RouterConfig(endpoints, routesList);

            var dbStoreProvider = new InMemoryDbStoreProvider();
            var storeProvider = new StoreProvider(dbStoreProvider);
            var messageStore = new MessageStore(storeProvider, CheckpointStore.Create(storeProvider), TimeSpan.MaxValue, false, 1800);
            var endpointExecutorFactory = new StoringAsyncEndpointExecutorFactory(endpointExecutorConfig, new AsyncEndpointExecutorOptions(1, TimeSpan.FromMilliseconds(10)), messageStore);

            var router = await Router.CreateAsync(Guid.NewGuid().ToString(), iotHubName, routerConfig, endpointExecutorFactory);

            var messageConverterProvider = new MessageConverterProvider(
                                                    new Dictionary<Type, IMessageConverter>()
                                                    {
                                                        { typeof(Twin), new TwinMessageConverter() },
                                                        { typeof(TwinCollection), new TwinCollectionMessageConverter() }
                                                    });

            var twinManager = TwinManager.CreateTwinManager(connectionManager, messageConverterProvider, Option.None<IStoreProvider>());
            var invokeMethodHandler = Mock.Of<IInvokeMethodHandler>();
            var subscriptionProcessor = new SubscriptionProcessor(connectionManager, invokeMethodHandler, deviceConnectivityManager);

            var edgeHub = new RoutingEdgeHub(router, routingMessageConverter, connectionManager, twinManager, edgeDeviceId, edgeModuleName, invokeMethodHandler, subscriptionProcessor, Mock.Of<IDeviceScopeIdentitiesCache>());

            var brokerConnector = new NullBrokerConnector(cloudProxyDispatcher);
            cloudProxyDispatcher.SetConnector(brokerConnector);
            cloudProxyDispatcher.BindEdgeHub(edgeHub);
            
            var connectionProvider = new ConnectionProvider(connectionManager, edgeHub, TimeSpan.FromSeconds(30));
            var authenticator = new NullAuthenticator();

            var edgeHubIdentity = new ModuleIdentity(iotHubName, edgeDeviceId, edgeModuleName);
            var tokenCredentials = new TokenCredentials(edgeHubIdentity, "qwerty", "test-product", Option.Some("test-model"), Option.None<string>(), false);
            var systemComponentProvider = new SystemComponentIdProvider(tokenCredentials);

            var connectionHandler = default(ConnectionHandler);
            connectionHandler = new ConnectionHandler(
                                            Task.FromResult<IConnectionProvider>(connectionProvider),
                                            Task.FromResult<IAuthenticator>(authenticator),
                                            identityProvider,
                                            systemComponentProvider,
                                            DeviceProxyFactory);

            DeviceProxy DeviceProxyFactory(IIdentity identity, bool isDirectClient)
            {
                return new DeviceProxy(identity, isDirectClient, connectionHandler, Mock.Of<ITwinHandler>(), Mock.Of<IModuleToModuleMessageHandler>(), Mock.Of<ICloud2DeviceMessageHandler>(), Mock.Of<IDirectMethodHandler>());
            }

            var cloud2DeviceMessageHandler = new Cloud2DeviceMessageHandler(connectionHandler);
            var moduleToModuleMessageHandler = new ModuleToModuleMessageHandler(connectionHandler, identityProvider, new ModuleToModuleResponseTimeout(TimeSpan.FromSeconds(10)));
            var directMethodHandler = new DirectMethodHandler(connectionHandler, identityProvider);
            var twinHandler = new TwinHandler(connectionHandler, identityProvider);

            var subscriptionChangeHandler = new SubscriptionChangeHandler(
                                                    cloud2DeviceMessageHandler,
                                                    moduleToModuleMessageHandler,
                                                    directMethodHandler,
                                                    twinHandler,
                                                    connectionHandler,
                                                    identityProvider);

            return (subscriptionChangeHandler, cloudProxyDispatcher, brokerConnector);
        }

        // This class is the one that is supposed to forward the MQTT messages to the broker.
        // Now instead it swallows the messages, and auto-acks the RPC calls going upstream.
        internal class NullBrokerConnector : IMqttBrokerConnector
        {
            BrokeredCloudProxyDispatcher dispatcher;
            Option<Action<RpcPacket>> spy;
            
            public NullBrokerConnector(BrokeredCloudProxyDispatcher dispatcher)
            {
                this.dispatcher = dispatcher;
            }

            public void SetPacketSpy(Action<RpcPacket> spy) => this.spy = Option.Some(spy);

            public Task EnsureConnected => Task.CompletedTask;
            public Task ConnectAsync(string serverAddress, int port) => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;
            public async Task<bool> SendAsync(string topic, byte[] payload, bool retain = false)
            {
                const string rpcCall = "$upstream/rpc/";
                if (topic.StartsWith(rpcCall))
                {
                    var guid = topic.Substring(rpcCall.Length);
                    await dispatcher.HandleAsync(new MqttPublishInfo("$downstream/rpc/ack/" + guid, new byte[0]));

                    var packet = default(RpcPacket);
                    using (var reader = new BsonDataReader(new MemoryStream(payload)))
                    {
                        var serializer = new JsonSerializer
                        {
                            ContractResolver = new DefaultContractResolver
                            {
                                NamingStrategy = new CamelCaseNamingStrategy()
                            }
                        };

                        packet = serializer.Deserialize<RpcPacket>(reader);
                    }

                    this.spy.ForEach(s => s(packet));
                }

                return true;
            }
        }
    }
}
