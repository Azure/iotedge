// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;

    public class MockKubeApiServer : IDisposable
    {
        readonly IWebHost webHost;

        public MockKubeApiServer(Func<HttpContext, Task<bool>> shouldNext, string resp = null, Action<ListenOptions> listenConfigure = null)
        {
            shouldNext = shouldNext ?? (_ => Task.FromResult(true));
            listenConfigure = listenConfigure ?? (_ => { });

            this.webHost = WebHost.CreateDefaultBuilder()
                .Configure(
                    app => app.Run(
                        async httpContext =>
                        {
                            if (await shouldNext(httpContext))
                            {
                                await httpContext.Response.WriteAsync(resp);
                            }
                        }))
                .UseKestrel(options => { options.Listen(IPAddress.Loopback, 0, listenConfigure); })
                .Build();

            this.webHost.Start();
        }

        public Uri Uri => this.webHost.ServerFeatures.Get<IServerAddressesFeature>().Addresses
            .Select(a => new Uri(a)).First();

        public static async Task WriteStreamLine(HttpContext httpContext, string reponseLine)
        {
            const string crlf = "\r\n";
            await httpContext.Response.WriteAsync(reponseLine.Replace(crlf, string.Empty));
            await httpContext.Response.WriteAsync(crlf);
            await httpContext.Response.Body.FlushAsync();
        }

        public void Dispose()
        {
            this.webHost.StopAsync();
            this.webHost.WaitForShutdown();
        }
    }
}
