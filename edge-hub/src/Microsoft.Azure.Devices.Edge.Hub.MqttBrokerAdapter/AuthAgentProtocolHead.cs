// Copyright (c) Microsoft. All rights reserve.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Hosting;


    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    using Microsoft.Extensions.Logging;

    public class AuthAgentProtocolHead : IProtocolHead
    {
        readonly IAuthenticator authenticator;
        readonly IUsernameParser usernameParser;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly AuthAgentProtocolHeadConfig config;

        private IWebHost host;

        public string Name => "AUTH";

        public AuthAgentProtocolHead(IAuthenticator authenticator, IUsernameParser usernameParser, IClientCredentialsFactory clientCredentialsFactory, AuthAgentProtocolHeadConfig config)
        {
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.usernameParser = Preconditions.CheckNotNull(usernameParser, nameof(usernameParser));
            this.clientCredentialsFactory = Preconditions.CheckNotNull(clientCredentialsFactory, nameof(clientCredentialsFactory));
            this.config = Preconditions.CheckNotNull(config);
        }

        public async Task StartAsync()
        {
            Events.Starting();

            var newHost = AuthAgentRequestHandler.CreateWebHostBuilder(this.authenticator, this.usernameParser, this.clientCredentialsFactory, this.config);
            if (Interlocked.CompareExchange(ref this.host, newHost, null) != null)
            {
                newHost.Dispose();
                Events.StartedWhenAlreadyRunning();
                throw new InvalidOperationException("Cannot start AuthAgentProtocolHead twice");
            }

            await this.host.StartAsync();

            Events.Started();
        }

        public async Task CloseAsync(CancellationToken token)
        {
            Events.Closing();

            var currentHost = Interlocked.Exchange(ref this.host, null);
            if (currentHost == null)
            {
                Events.ClosedWhenNotRunning();
                return;
            }

            await currentHost.StopAsync();

            Events.Closed();
        }

        public void Dispose() => this.CloseAsync(CancellationToken.None).Wait();

        static class Events
        {
            const int IdStart = AuthAgentEventIds.AuthAgentProtocolHead;
            static readonly ILogger Log = Logger.Factory.CreateLogger<AuthAgentRequestHandler>();

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
