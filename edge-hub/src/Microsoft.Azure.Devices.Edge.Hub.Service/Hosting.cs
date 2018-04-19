// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using Autofac;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Hosting
    {
        const int SslPortNumber = 443;

        Hosting(IWebHost webHost, IContainer container)
        {
            this.WebHost = webHost;
            this.Container = container;
        }

        public static Hosting Initialize(string certPath)
        {
            var sslCert = new X509Certificate2(Preconditions.CheckNonWhiteSpace(certPath, nameof(certPath)));
            IWebHostBuilder webHostBuilder = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(!Socket.OSSupportsIPv6 ? IPAddress.Any : IPAddress.IPv6Any, SslPortNumber, listenOptions =>
                    {
                        listenOptions.UseHttps(sslCert);
                    });
                })
                .UseStartup<Startup>();
            IWebHost webHost = webHostBuilder.Build();
            IContainer container = webHost.Services.GetService(typeof(IStartup)) is Startup startup ? startup.Container : null;
            return new Hosting(webHost, container);
        }

        public IContainer Container { get; }

        public IWebHost WebHost { get; }
    }
}
