// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class HttpProtocolHead : IProtocolHead
    {
        readonly IWebHost webHost;

        public HttpProtocolHead(IWebHost webHost)
        {
            this.webHost = Preconditions.CheckNotNull(webHost, nameof(webHost));
        }

        public string Name => "HTTP";

        public async Task CloseAsync(CancellationToken token)
        {
            Events.Closing();
            await this.webHost.StopAsync(token);
            Events.Closed();
        }

        public void Dispose() => this.webHost?.Dispose();

        public async Task StartAsync()
        {
            Events.Starting();
            await this.webHost.StartAsync();
            Events.Started();
        }

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

            public static void Closed()
            {
                Log.LogInformation((int)EventIds.Closed, "Closed HTTP head");
            }

            public static void Closing()
            {
                Log.LogInformation((int)EventIds.Closing, "Closing HTTP head");
            }

            public static void Started()
            {
                Log.LogInformation((int)EventIds.Started, "Started HTTP head");
            }

            public static void Starting()
            {
                Log.LogInformation((int)EventIds.Starting, "Starting HTTP head");
            }
        }
    }
}
