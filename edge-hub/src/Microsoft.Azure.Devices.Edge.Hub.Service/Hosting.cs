// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System.Net;
    using System.Net.Sockets;
    using Autofac;
    using Microsoft.AspNetCore.Hosting;

    public class Hosting
    {
        Hosting(IWebHost webHost, IContainer container)
        {
            this.WebHost = webHost;
            this.Container = container;
        }

        public static Hosting Initialize(int port)
        {
            IWebHostBuilder webHostBuilder = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(!Socket.OSSupportsIPv6 ? IPAddress.Any : IPAddress.IPv6Any, port, listenOptions =>
                    {
                        listenOptions.UseHttps(ServerCertificateCache.X509Certificate);
                    });
                })
                .UseSockets()
                .UseStartup<Startup>();
            IWebHost webHost = webHostBuilder.Build();
            IContainer container = webHost.Services.GetService(typeof(IStartup)) is Startup startup ? startup.Container : null;
            return new Hosting(webHost, container);
        }

        public IContainer Container { get; }

        public IWebHost WebHost { get; }
    }
}
