// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using Autofac;
    using Microsoft.AspNetCore.Hosting;
    using System.Security.Cryptography.X509Certificates;

    public class Hosting
    {
        IWebHost webHost;
        Startup startup;

        public IContainer Container => this.startup.Container;
        
        public void Initialize(X509Certificate2 sslCert, string url)
        {
            IWebHostBuilder webHostBuilder = new WebHostBuilder()
                .UseKestrel(options => options.UseHttps(sslCert))
                .UseUrls(url)
                .UseStartup<Startup>();
            this.webHost = webHostBuilder.Build();

            this.startup = this.webHost.Services.GetService(typeof(IStartup)) as Startup;
        }

        public void Start() => this.webHost.Start();
    }
}