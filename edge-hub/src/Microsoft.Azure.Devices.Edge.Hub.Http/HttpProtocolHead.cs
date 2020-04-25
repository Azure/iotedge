// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class HttpProtocolHead : IProtocolHead
    {
        readonly IHost host;

        public HttpProtocolHead(IHost host)
        {
            this.host = Preconditions.CheckNotNull(host, nameof(host));
        }

        public string Name => "HTTP";

        public async Task StartAsync()
        {
            Events.Starting();
            await this.host.StartAsync();
            Events.Started();
        }

        public async Task CloseAsync(CancellationToken token)
        {
            Events.Closing();
            await this.host.StopAsync(token);
            Events.Closed();
        }

        public void Dispose() => this.host?.Dispose();

        static class Events
        {
            const int IdStart = HttpEventIds.HttpProtocolHead;
            static readonly ILogger Log = Logger.Factory.CreateLogger<HttpProtocolHead>();

            enum EventIds
            {
                Starting = IdStart,
                Started,
                Closing,
                Closed
            }

            public static void Starting()
            {
                Log.LogInformation((int)EventIds.Starting, "Starting HTTP head");
            }

            public static void Started()
            {
                Log.LogInformation((int)EventIds.Started, "Started HTTP head");
            }

            public static void Closing()
            {
                Log.LogInformation((int)EventIds.Closing, "Closing HTTP head");
            }

            public static void Closed()
            {
                Log.LogInformation((int)EventIds.Closed, "Closed HTTP head");
            }
        }
    }
}
