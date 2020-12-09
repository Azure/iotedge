// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    public class AuthAgentProtocolHead : IProtocolHead
    {
        readonly IAuthenticator authenticator;
        readonly IMetadataStore metadataStore;
        readonly IUsernameParser usernameParser;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly ISystemComponentIdProvider systemComponentIdProvider;
        readonly AuthAgentProtocolHeadConfig config;
        readonly object guard = new object();

        Option<IWebHost> host;

        public string Name => "AUTH";

        public AuthAgentProtocolHead(
                    IAuthenticator authenticator,
                    IMetadataStore metadataStore,
                    IUsernameParser usernameParser,
                    IClientCredentialsFactory clientCredentialsFactory,
                    ISystemComponentIdProvider systemComponentIdProvider,
                    AuthAgentProtocolHeadConfig config)
        {
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.metadataStore = Preconditions.CheckNotNull(metadataStore, nameof(metadataStore));
            this.usernameParser = Preconditions.CheckNotNull(usernameParser, nameof(usernameParser));
            this.clientCredentialsFactory = Preconditions.CheckNotNull(clientCredentialsFactory, nameof(clientCredentialsFactory));
            this.systemComponentIdProvider = Preconditions.CheckNotNull(systemComponentIdProvider);
            this.config = Preconditions.CheckNotNull(config);
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
                                        this.metadataStore,
                                        this.usernameParser,
                                        this.clientCredentialsFactory,
                                        this.systemComponentIdProvider,
                                        this.config));
                }
            }

            await this.host.Expect(() => new Exception("No AUTH host instance found to start"))
                           .StartAsync();

            Events.Started();
        }

        public async Task CloseAsync(CancellationToken token)
        {
            Events.Closing();

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
                            IMetadataStore metadataStore,
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
                          .ConfigureServices(s => s.TryAddSingleton(metadataStore))
                          .ConfigureServices(s => s.TryAddSingleton(usernameParser))
                          .ConfigureServices(s => s.TryAddSingleton(clientCredentialsFactory))
                          .ConfigureServices(s => s.TryAddSingleton(systemComponentIdProvider))
                          .ConfigureServices(s => s.TryAddSingleton(config))
                          .ConfigureServices(s => s.AddControllers().AddNewtonsoftJson())
                          .ConfigureLogging(c => c.ClearProviders())
                          .Build();
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
                StartedWhenAlreadyRunning
            }

            public static void Starting() => Log.LogInformation((int)EventIds.Starting, "Starting AUTH head");
            public static void Started() => Log.LogInformation((int)EventIds.Started, "Started AUTH head");
            public static void Closing() => Log.LogInformation((int)EventIds.Closing, "Closing AUTH head");
            public static void Closed() => Log.LogInformation((int)EventIds.Closed, "Closed AUTH head");
            public static void ClosedWhenNotRunning() => Log.LogInformation((int)EventIds.ClosedWhenNotRunning, "Closed AUTH head when it was not running");
            public static void StartedWhenAlreadyRunning() => Log.LogWarning((int)EventIds.StartedWhenAlreadyRunning, "Started AUTH head when it was already running");
        }
    }
}
