// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using Autofac;
    using Microsoft.AspNetCore.Hosting;

    public class Hosting
    {
        const int SslPortNumber = 443;

        IWebHost webHost;
        Startup startup;

        public IContainer Container => this.startup.Container;

        public void Initialize(X509Certificate2 sslCert)
        {
            IWebHostBuilder webHostBuilder = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.IPv6Any, SslPortNumber, listenOptions =>
                    {
                        listenOptions.UseHttps(sslCert);
                    });
                })
                .UseStartup<Startup>();
            this.webHost = webHostBuilder.Build();

            this.startup = this.webHost.Services.GetService(typeof(IStartup)) as Startup;
        }

        public void Start() => this.webHost.Start();
    }
}
