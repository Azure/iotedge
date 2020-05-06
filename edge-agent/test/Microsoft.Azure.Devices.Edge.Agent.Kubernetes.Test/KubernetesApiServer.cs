// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using k8s;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;

    public class KubernetesApiServer : IDisposable
    {
        readonly IWebHost webHost;

        public KubernetesApiServer(Func<HttpContext, Task<bool>> shouldNext, string resp = null, Action<ListenOptions> listenConfigure = null)
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

        public string Uri
        {
            get
            {
                return this.webHost.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First();
            }
        }

        public void Dispose()
        {
            this.webHost.StopAsync();
            this.webHost.WaitForShutdown();
        }

        public static KubernetesApiServer Watch<T>(IEnumerable<Watcher<T>.WatchEvent> events) =>
            new KubernetesApiServer(
                async context =>
                {
                    foreach (var @event in events)
                    {
                        await context.Write(@event);
                    }

                    return false;
                });
    }
}
