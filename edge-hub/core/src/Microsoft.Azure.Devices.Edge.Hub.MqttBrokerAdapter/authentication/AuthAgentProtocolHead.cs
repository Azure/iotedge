// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.handlers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    public class AuthAgentProtocolHead : AbstractNotificationHandler<bool>, IProtocolHead
    {
        const string Topic = "$internal/edgehubcore";
        static readonly string Payload = "\"AuthAgentProtocolHead started.\"";

        readonly IAuthenticator authenticator;
        readonly IUsernameParser usernameParser;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly ISystemComponentIdProvider systemComponentIdProvider;
        readonly AuthAgentProtocolHeadConfig config;
        readonly object guard = new object();

        bool sendNotificationOnce;
        Option<IWebHost> host;

        public string Name => "AUTH";

        public AuthAgentProtocolHead(
                    IAuthenticator authenticator,
                    IUsernameParser usernameParser,
                    IClientCredentialsFactory clientCredentialsFactory,
                    ISystemComponentIdProvider systemComponentIdProvider,
                    AuthAgentProtocolHeadConfig config,
                    IMqttBrokerConnector mqttBrokerConnector) : base()
        {
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.usernameParser = Preconditions.CheckNotNull(usernameParser, nameof(usernameParser));
            this.clientCredentialsFactory = Preconditions.CheckNotNull(clientCredentialsFactory, nameof(clientCredentialsFactory));
            this.systemComponentIdProvider = Preconditions.CheckNotNull(systemComponentIdProvider);
            this.config = Preconditions.CheckNotNull(config);
            SetConnector(mqttBrokerConnector);
        }

        public async Task StartAsync()
        {
            Events.Starting();

            lock (this.guard)
            {
                if (this.host.HasValue)
                {
                    Events.StartedWhenAlreadyRunning();
                    throw new InvalidOperationException("Cannot start AuthAgentProtocolHead twice");
                }
                else
                {
                    this.host = Option.Some(
                                    CreateWebHostBuilder(
                                        this.authenticator,
                                        this.usernameParser,
                                        this.clientCredentialsFactory,
                                        this.systemComponentIdProvider,
                                        this.config));
                }
            }

            await this.host.Expect(() => new Exception("No AUTH host instance found to start"))
                           .StartAsync();
            await NotifyAsync(true);
            Events.Started();
        }

        public async Task CloseAsync(CancellationToken token)
        {
            Events.Closing();

            sendNotificationOnce = false;

            Option<IWebHost> hostToStop;
            lock (this.guard)
            {
                hostToStop = this.host;
                this.host = Option.None<IWebHost>();
            }

            await hostToStop.Match(
                async h => await h.StopAsync(),
                () =>
                {
                    Events.ClosedWhenNotRunning();
                    throw new InvalidOperationException("Cannot stop AuthAgentProtocolHead when not running");
                });

            Events.Closed();
        }

        public void Dispose()
        {
            if (this.host.HasValue)
            {
                this.CloseAsync(CancellationToken.None).Wait();
            }
        }

        static IWebHost CreateWebHostBuilder(
                            IAuthenticator authenticator,
                            IUsernameParser usernameParser,
                            IClientCredentialsFactory clientCredentialsFactory,
                            ISystemComponentIdProvider systemComponentIdProvider,
                            AuthAgentProtocolHeadConfig config)
        {
            return WebHost.CreateDefaultBuilder()
                          .UseStartup<AuthAgentStartup>()
                          .UseKestrel(serverOptions => serverOptions.Limits.MaxRequestBufferSize = 64 * 1024)
                          .UseUrls($"http://*:{config.Port}")
                          .ConfigureServices(s => s.TryAddSingleton(authenticator))
                          .ConfigureServices(s => s.TryAddSingleton(usernameParser))
                          .ConfigureServices(s => s.TryAddSingleton(clientCredentialsFactory))
                          .ConfigureServices(s => s.TryAddSingleton(systemComponentIdProvider))
                          .ConfigureServices(s => s.TryAddSingleton(config))
                          .ConfigureServices(s => s.AddControllers().AddNewtonsoftJson())
                          .ConfigureLogging(c => c.ClearProviders())
                          .Build();
        }

        public override Task StoreNotificationAsync(bool notification)
        {
            sendNotificationOnce = true;
            return Task.FromResult(true);
        }

        public override Task<IEnumerable<Message>> ConvertStoredNotificationsToMessagesAsync()
        {
            IEnumerable<Message> messages;
            if (sendNotificationOnce)
            {
                messages = new[] { new Message(Topic, Payload) };
            }
            else
            {
                messages = new Message[0];
            }

            return Task.FromResult(messages);
        }

        public override Task<IEnumerable<Message>> ConvertNotificationToMessagesAsync(bool notification)
        {
            IEnumerable<Message> messages = new[] { new Message(Topic, Payload) };
            return Task.FromResult(messages);
        }

        static class Events
        {
            const int IdStart = AuthAgentEventIds.AuthAgentProtocolHead;
            static readonly ILogger Log = Logger.Factory.CreateLogger<AuthAgentProtocolHead>();

            enum EventIds
            {
                Starting = IdStart,
                Started,
                Closing,
                Closed,
                ClosedWhenNotRunning,
                StartedWhenAlreadyRunning,
                NotifyingBroker,
                NotifiedBroker,
                NotifyBrokerFailed
            }

            public static void Starting() => Log.LogInformation((int)EventIds.Starting, "Starting AUTH head");
            public static void Started() => Log.LogInformation((int)EventIds.Started, "Started AUTH head");
            public static void Closing() => Log.LogInformation((int)EventIds.Closing, "Closing AUTH head");
            public static void Closed() => Log.LogInformation((int)EventIds.Closed, "Closed AUTH head");
            public static void ClosedWhenNotRunning() => Log.LogInformation((int)EventIds.ClosedWhenNotRunning, "Closed AUTH head when it was not running");
            public static void StartedWhenAlreadyRunning() => Log.LogWarning((int)EventIds.StartedWhenAlreadyRunning, "Started AUTH head when it was already running");
            public static void NotifyingBroker() => Log.LogInformation((int)EventIds.NotifyingBroker, "Notifying broker auth agent started.");
            public static void NotifiedBroker() => Log.LogInformation((int)EventIds.NotifiedBroker, "Notified broker auth agent started.");
            public static void NotifyBrokerFailed() => Log.LogInformation((int)EventIds.NotifyBrokerFailed, "Failed to notify broker auth agent started.");

        }
    }
}
