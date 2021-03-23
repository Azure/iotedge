// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Moq;
    using Xunit;

    using IMessage = Core.IMessage;

    // This test is to ensure that edgeHub is able to consistently track that which clients are connected
    // and what is their subscription state.
    // EdgeHub, when clients connect through the MQTT Broker, it does not "see" clients directly. The
    // broker sends connection events, listing the connected clients, and the broker also sends subscription
    // events when a client subscribes/unsubscribes.
    // In nested scenario, edgeHub does not even receive those events directly. In that case child-edgeHub
    // forwards certain operations (e.g. client_1 subscribed to something). As the parent MQTT Broker sees
    // only the child-edgeHub as children (not the clients connected to child-edgeHub), all the subscription
    // events are aggregated into one notification (as child-edgeHub subscribes in name of every of its clients)
    //
    // The following test generates clients both direct and nested. Then these clients generate thousands of
    // events, including messages an subscription/unsubscriptions. During the test it randomly checks that the
    // current state of edgeHub is what it is supposed to be. It does not check the status after every operation
    // to save processing time - it checks only ~20 percent of the events. It does not mean that it easily misses
    // problems, because older status changes still have effect (subscribing to M2M 3 iterations ago still should
    // in effect at the current iteration.
    //
    // One of the main classes below is the "Playbook" class. It pre-generates random subscription/unsubscription
    // events and moments for sending messages. Every test client has a playbook. Then the test start playing the
    // playbooks for the clients with every iteraton.
    //
    // At random moments (~20% of the iteration) the code calculates the expected current state and compares it
    // to the real state of the system.

    [Integration]
    public class NestedConnectionTrackingTest
    {
        const int PlaybookLength = 10000;
        const int PlaybookSectionMinLen = 10;
        const int PlaybookSectionMaxLen = 100;

        static readonly string iotHubName = "testHub";
        static readonly string edgeModuleName = "$edgeHub";

        // A TestClient contains two important states:
        // - whether edgeHub should have noticed it according to the client activity
        // - a 'playbook' which describes when to subscribe/unsubscribe/send messages
        class TestClient
        {
            public TestClient(bool isDirect, IIdentity identity)
            {
                this.IsDirect = isDirect;
                this.Identity = identity;
                this.Playbook = new Playbook(PlaybookLength, PlaybookSectionMinLen, PlaybookSectionMaxLen, identity is ModuleIdentity);

                this.IsNoticed = false;
            }

            public bool IsDirect { get; }
            public IIdentity Identity { get; }
            public Playbook Playbook { get; }

            public bool IsNoticed { get; private set; }
            public void SetNoticed() => this.IsNoticed = true;
        }

        enum SubscriptionOrMessage
        {
            // Keep 'Message' as first, as in the playbook it is referred as 0
            Message,
            TwinResponse,
            DesiredPropertyUpdates,
            C2D,
            DirectMethod,
            Last // Keep this here as the numeric value of this is used in for loops
        }

        // The PLaybook class controls the behavior of a test client. It is a pre-generated list of actions.
        // A client can have different subscriptions and can send messages. An easy way to picture a playbook
        // is to see it as a punch card. Every column of the punch card represends a subscription (e.g. C2D)
        // or sending a message. This class then generates a punchcard where a dot tells that the client must
        // subscribe/send message:
        //
        // +-----------------------------
        // | Msg :    *        *   *
        // | C2d :        **********
        // | Twin:  ****************
        // +-----------------------------
        //
        // On the example above, first a twin subscription will be made, then a bit later a message will be
        // sent out, then a C2D subscription will be made, then at the same time the client unsubscribes from
        // C2D and twin, and also sends a message.
        class Playbook
        {
            static Random rnd = new Random(2468742);

            readonly int phases;
            readonly bool[,] playbook;

            public Playbook(int phases, int minDuration, int maxDuration, bool isModule)
            {
                this.phases = phases;
                this.playbook = new bool[(int)SubscriptionOrMessage.Last, phases];

                GenerateSubscriptions(minDuration, maxDuration, isModule);
                GenerateMessages(maxDuration);
            }

            public bool IsActive(SubscriptionOrMessage eventType, int phase)
            {
                if (phase < 0 || phase >= this.phases)
                {
                    return false;
                }

                return this.playbook[(int)eventType, phase];
            }

            void GenerateSubscriptions(int minDuration, int maxDuration, bool isModule)
            {
                // start from 1 as for 'Message' a different loop makes the generation
                for (int i = 1; i < (int)SubscriptionOrMessage.Last; i++)
                {
                    // Modules cannot handle C2D, so skip generating those
                    if (i == (int)SubscriptionOrMessage.C2D && isModule)
                    {
                        continue;
                    }

                    var pos = 0;
                    while (pos < this.phases)
                    {
                        var offLen = Math.Min(rnd.Next(minDuration, maxDuration), this.phases - pos);

                        for (var n = 0; n < offLen; n++)
                        {
                            this.playbook[i, pos++] = false;
                        }

                        if (pos < this.phases)
                        {
                            var onLen = Math.Min(rnd.Next(minDuration, maxDuration), phases - pos);

                            for (var n = 0; n < onLen; n++)
                            {
                                this.playbook[i, pos++] = true;
                            }
                        }
                    }
                }
            }

            void GenerateMessages(int maxDuration)
            {
                var pos = 0;
                while (pos < this.phases)
                {
                    var nextPos = Math.Min(rnd.Next(maxDuration), this.phases - pos);
                    for (var n = 0; n < nextPos; n++)
                    {
                        this.playbook[0, pos++] = false;
                    }

                    if (pos < this.phases)
                    {
                        this.playbook[0, pos++] = true;
                    }
                }
            }
        }

        [Fact]
        public async Task ConnectionsAndSubscriptionsAreBeingTracked()
        {
            // Generating the necessary edgeHub components and the test clients
            var (connectionManager, connectionHandler, subscriptionChangeHandler, telemetryHandler) = await SetupEdgeHub("something");
            var clients = GenerateClients(100, 0.5, 0.5);

            var rnd = new Random(548196703);
            var subscriptionTypes = new[] { SubscriptionOrMessage.C2D, SubscriptionOrMessage.DesiredPropertyUpdates, SubscriptionOrMessage.DirectMethod, SubscriptionOrMessage.TwinResponse };

            // Start playing the playbook. At every iteration it executes the operations (e.g. subscribe to twin results) that the playbook of a
            // given client dictates.
            for (var phase = 0; phase < PlaybookLength; phase++)
            {
                // get a randomized order of clients so the messages are more stochastic
                clients.Shuffle(rnd);

                // Direct clients can send their subscriptions immediately, however for nested clients we collect them and send a single
                // update, as in this case edgeHub sends a single event describing all the nested clients. 
                var edgeHubSubscriptions = new List<string>();

                foreach (var client in clients)
                {
                    // This is just to avoid sending the subscriptions always the same order, e.g. always twin first, then c2d
                    subscriptionTypes.Shuffle(rnd);

                    if (client.IsDirect)
                    {
                        var hasChanged = false;
                        var currentSubscriptions = new List<string>();
                        foreach (var sub in subscriptionTypes)
                        {
                            // we are interested only in changes. Note, that the playbook handles the call with -1 (phase=0), so no error at the next line
                            if (client.Playbook.IsActive(sub, phase) ^ client.Playbook.IsActive(sub, phase-1))
                            {
                                hasChanged = true;
                            }

                            if (client.Playbook.IsActive(sub, phase))
                            {                                
                                currentSubscriptions.Add(SubscriptionGenerator[sub](true, client.Identity));
                            }
                        }

                        if (hasChanged)
                        {
                            client.SetNoticed();
                            var subscriptionEvent = $"[{currentSubscriptions.Select(s => $"\"{s}\"").Join(", ")}]";
                            await subscriptionChangeHandler.HandleAsync(new MqttPublishInfo($"$edgehub/{client.Identity.Id}/subscriptions", Encoding.UTF8.GetBytes(subscriptionEvent)));
                        }

                        if (client.Playbook.IsActive(SubscriptionOrMessage.Message, phase))
                        {
                            client.SetNoticed();
                            await telemetryHandler.HandleAsync(new MqttPublishInfo($"$edgehub/{client.Identity.Id}/messages/events", Encoding.UTF8.GetBytes("hello")));
                        }
                    }
                    else
                    {
                        foreach (var sub in subscriptionTypes)
                        {
                            // just store all the subscribed topics. Note, that this code does not care if the result is the same as previously,
                            // however resending an event twice should not cause problems for edgeHub
                            if (client.Playbook.IsActive(sub, phase))
                            {
                                client.SetNoticed();
                                edgeHubSubscriptions.Add(SubscriptionGenerator[sub](false, client.Identity));
                            }
                        }

                        if (client.Playbook.IsActive(SubscriptionOrMessage.Message, phase))
                        {
                            client.SetNoticed();
                            await telemetryHandler.HandleAsync(new MqttPublishInfo($"$iothub/{client.Identity.Id}/messages/events", Encoding.UTF8.GetBytes("hello")));
                        }
                    }
                }

                var edgeHubsubscriptionEvent = $"[{edgeHubSubscriptions.Select(s => $"\"{s}\"").Join(", ")}]";
                await subscriptionChangeHandler.HandleAsync(new MqttPublishInfo("$edgehub/nested_dev/$edgeHub/subscriptions", Encoding.UTF8.GetBytes(edgeHubsubscriptionEvent)));

                // we do a phase check at around %20 of the steps
                if (rnd.NextDouble() < 0.2 || phase+1 == PlaybookLength)
                {
                    var clientsShouldBeKnown = new HashSet<IIdentity>(clients.Where(c => c.IsNoticed).Select(c => c.Identity));

                    var started = DateTime.Now;
                    var knownClientsAreOk = false;

                    // Calculating the actual state in a loop, that is because it may take time till the subscription events are get processed
                    do
                    {
                        var clientsKnown = new HashSet<IIdentity>((connectionHandler.AsPrivateAccessible().knownConnections as ConcurrentDictionary<IIdentity, IDeviceListener>).Keys);
                        knownClientsAreOk = clientsKnown.SetEquals(clientsShouldBeKnown);

                        if (!knownClientsAreOk)
                        {
                            await Task.Delay(500);
                        }
                    }
                    while (!knownClientsAreOk && DateTime.Now - started < TimeSpan.FromSeconds(5));

                    Assert.True(knownClientsAreOk);

                    started = DateTime.Now;
                    var subscriptionsAreOk = false;
                    foreach (var client in clients.Where(c => c.IsNoticed))
                    {
                        var expectedSubscriptions = default(HashSet<DeviceSubscription>);
                        var actualSubscriptions = default(HashSet<DeviceSubscription>);
                        do
                        {

                            expectedSubscriptions = new HashSet<DeviceSubscription>();
                            foreach (var sub in subscriptionTypes)
                            {
                                if (client.Playbook.IsActive(sub, phase))
                                {
                                    expectedSubscriptions.Add(
                                        sub switch
                                        {
                                            SubscriptionOrMessage.C2D => DeviceSubscription.C2D,
                                            SubscriptionOrMessage.DesiredPropertyUpdates => DeviceSubscription.DesiredPropertyUpdates,
                                            SubscriptionOrMessage.DirectMethod => DeviceSubscription.Methods,
                                            SubscriptionOrMessage.TwinResponse => DeviceSubscription.TwinResponse,
                                            _ => DeviceSubscription.Unknown,
                                        });
                                }
                            }

                            actualSubscriptions = new HashSet<DeviceSubscription>(
                                                            connectionManager.GetSubscriptions(client.Identity.Id).Expect(() => new Exception("client should be known"))
                                                                             .Where(s => s.Value == true)
                                                                             .Select(s => s.Key));

                            subscriptionsAreOk = expectedSubscriptions.SetEquals(actualSubscriptions);

                            if (!subscriptionsAreOk)
                            {
                                await Task.Delay(500);
                            }
                        }
                        while (!subscriptionsAreOk && DateTime.Now - started < TimeSpan.FromSeconds(5));

                        Assert.True(subscriptionsAreOk);
                    }
                }
            }
        }

        List<TestClient> GenerateClients(int clientCount, double moduleRatio, double directRatio)
        {
            var rnd = new Random(921752);
            var result = new List<TestClient>();

            for (var i = 0; i < clientCount; i++)
            {
                bool isModule = moduleRatio > rnd.NextDouble();
                bool isDirect = directRatio > rnd.NextDouble();

                var identity = isModule ? new ModuleIdentity(iotHubName, "device_" + i.ToString(), "module_" + i.ToString()) as IIdentity
                                        : new DeviceIdentity(iotHubName, "device_" + i.ToString()) as IIdentity;

                var newClient = new TestClient(isDirect, identity);

                result.Add(newClient);
            }

            return result;
        }

        static Dictionary<SubscriptionOrMessage, Func<bool, IIdentity, string>> SubscriptionGenerator = new Dictionary<SubscriptionOrMessage, Func<bool, IIdentity, string>>()
            {
                [SubscriptionOrMessage.C2D] = GetC2DSubscription,
                [SubscriptionOrMessage.DesiredPropertyUpdates] = GetDesiredProperySubscription,
                [SubscriptionOrMessage.DirectMethod] = GetDirectMethodSubscription,
                [SubscriptionOrMessage.TwinResponse] = GetTwinResponseSubscription,
        };

        static string GetC2DSubscription(bool isDirect, IIdentity identity) => GetSubscription(isDirect, identity, "messages/c2d/post/#");
        static string GetDesiredProperySubscription(bool isDirect, IIdentity identity) => GetSubscription(isDirect, identity, "twin/desired/#");
        static string GetDirectMethodSubscription(bool isDirect, IIdentity identity) => GetSubscription(isDirect, identity, "methods/post/#");
        static string GetTwinResponseSubscription(bool isDirect, IIdentity identity) => GetSubscription(isDirect, identity, "twin/res/#");

        static string GetSubscription(bool isDirect, IIdentity identity, string tail)
        {
            var dialect = isDirect
                            ? "$edgehub"
                            : "$iothub";

            return $"{dialect}/{identity.Id}/{tail}";
        }

        static async Task<(ConnectionManager, IConnectionRegistry, IMessageConsumer, IMessageConsumer)> SetupEdgeHub(string edgeDeviceId)
        {
            Routing.UserMetricLogger = NullRoutingUserMetricLogger.Instance;
            Routing.PerfCounter = NullRoutingPerfCounter.Instance;
            Routing.UserAnalyticsLogger = NullUserAnalyticsLogger.Instance;

            
            var identityProvider = new IdentityProvider(iotHubName);
            var deviceConnectivityManager = Mock.Of<IDeviceConnectivityManager>();

            var connectionManager = new ConnectionManager(new NullCloudConnectionProvider(), Mock.Of<ICredentialsCache>(), new IdentityProvider(iotHubName), deviceConnectivityManager);
            var routingMessageConverter = new RoutingMessageConverter();
            var routeFactory = new EdgeRouteFactory(new EndpointFactory(connectionManager, routingMessageConverter, edgeDeviceId, 10, 10, true));
            var routesList = new [] {routeFactory.Create("FROM /messages INTO $upstream") };
            var endpoints = routesList.Select(r => r.Endpoint);
            var routerConfig = new RouterConfig(endpoints, routesList);

            var dbStoreProvider = new InMemoryDbStoreProvider();
            var storeProvider = new StoreProvider(dbStoreProvider);
            var messageStore = new MessageStore(storeProvider, CheckpointStore.Create(storeProvider), TimeSpan.MaxValue, false, 1800);
            var endpointExecutorConfig = new EndpointExecutorConfig(TimeSpan.FromSeconds(60), new FixedInterval(0, TimeSpan.FromSeconds(1)), TimeSpan.FromHours(1), true);
            var endpointExecutorFactory = new StoringAsyncEndpointExecutorFactory(endpointExecutorConfig, new AsyncEndpointExecutorOptions(1, TimeSpan.FromMilliseconds(10)), messageStore);
            var router = await Router.CreateAsync(Guid.NewGuid().ToString(), iotHubName, routerConfig, endpointExecutorFactory);

            var twinManager = new TwinManager(connectionManager, new TwinCollectionMessageConverter(), new TwinMessageConverter(), Option.None<IEntityStore<string, TwinInfo>>());
            var invokeMethodHandler = Mock.Of<IInvokeMethodHandler>();
            var subscriptionProcessor = new SubscriptionProcessor(connectionManager, invokeMethodHandler, deviceConnectivityManager);

            var edgeHub = new RoutingEdgeHub(router, routingMessageConverter, connectionManager, twinManager, edgeDeviceId, edgeModuleName, invokeMethodHandler, subscriptionProcessor, Mock.Of<IDeviceScopeIdentitiesCache>());

            var connectionProvider = new ConnectionProvider(connectionManager, edgeHub, TimeSpan.FromSeconds(30));
            
            var edgeHubIdentity = new ModuleIdentity(iotHubName, edgeDeviceId, edgeModuleName);
            var tokenCredentials = new TokenCredentials(edgeHubIdentity, "abc", "test-prod", Option.Some("test-model"), Option.None<string>(), false);
            var systemComponentProvider = new SystemComponentIdProvider(tokenCredentials);

            var connectionHandler = default(ConnectionHandler);
            var authenticator = new NullAuthenticator();
            connectionHandler = new ConnectionHandler(
                                            Task.FromResult<IConnectionProvider>(connectionProvider),
                                            Task.FromResult<IAuthenticator>(authenticator),
                                            identityProvider,
                                            systemComponentProvider,
                                            DeviceProxyFactory);

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

            var telemetryHandler = new TelemetryHandler(connectionHandler, identityProvider);

            return (connectionManager, connectionHandler, subscriptionChangeHandler, telemetryHandler);

            DeviceProxy DeviceProxyFactory(IIdentity identity, bool isDirectClient) =>
                new DeviceProxy(
                        identity,
                        isDirectClient,
                        connectionHandler,
                        Mock.Of<ITwinHandler>(),
                        Mock.Of<IModuleToModuleMessageHandler>(),
                        Mock.Of<ICloud2DeviceMessageHandler>(),
                        Mock.Of<IDirectMethodHandler>());
        }

        class NullCloudProxy : ICloudProxy
        {
            public bool IsActive => true;
            public Task<bool> CloseAsync() => Task.FromResult(true);
            public Task<bool> OpenAsync() => Task.FromResult(true);
            public Task SendMessageAsync(IMessage message) => Task.CompletedTask;
            public Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages) => Task.CompletedTask;
            public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage) => Task.CompletedTask;
            public Task<IMessage> GetTwinAsync() => Task.FromResult<IMessage>(new EdgeMessage(new byte[0], new Dictionary<string, string>(), new Dictionary<string, string>()));

            public Task SendFeedbackMessageAsync(string messageId, FeedbackStatus feedbackStatus) => Task.CompletedTask;

            public Task SetupCallMethodAsync() => Task.CompletedTask;
            public Task RemoveCallMethodAsync() => Task.CompletedTask;
            public Task SetupDesiredPropertyUpdatesAsync() => Task.CompletedTask;
            public Task RemoveDesiredPropertyUpdatesAsync() => Task.CompletedTask;
            public Task StartListening() => Task.CompletedTask;
            public Task RemoveTwinResponseAsync() => Task.CompletedTask;
            public Task StopListening() => Task.CompletedTask;
        }

        public class NullCloudConnection : ICloudConnection
        {
            ICloudProxy cloudProxy;

            public NullCloudConnection(ICloudProxy cloudProxy) => this.cloudProxy = cloudProxy;
            public Option<ICloudProxy> CloudProxy => Option.Some(this.cloudProxy);
            public bool IsActive => this.cloudProxy.IsActive;
            public Task<bool> CloseAsync() => Task.FromResult(false);
        }

        public class NullCloudConnectionProvider : ICloudConnectionProvider
        {
            public void BindEdgeHub(IEdgeHub edgeHub)
            {
            }

            public Task<Try<ICloudConnection>> Connect(IClientCredentials clientCredentials, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
                    => Task.FromResult<Try<ICloudConnection>>(Try.Success<ICloudConnection>(new NullCloudConnection(new NullCloudProxy())));
            public Task<Try<ICloudConnection>> Connect(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
                    => Task.FromResult<Try<ICloudConnection>>(Try.Success<ICloudConnection>(new NullCloudConnection(new NullCloudProxy())));
        }

        public class NullAuthenticator : IAuthenticator
        {
            public Task<bool> AuthenticateAsync(IClientCredentials identity) => Task.FromResult(true);
            public Task<bool> ReauthenticateAsync(IClientCredentials identity) => Task.FromResult(true);
        }
    }

    static class ListShuffleExtensions
    {
        public static void Shuffle<T>(this IList<T> list, Random rnd)
        {
            for (var i = list.Count; i > 0; i--)
            {
                list.Swap(0, rnd.Next(0, i));
            }
        }

        public static void Swap<T>(this IList<T> list, int i, int j)
        {
            var temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
