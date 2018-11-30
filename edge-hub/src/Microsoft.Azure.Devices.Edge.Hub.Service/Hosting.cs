// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using Autofac;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public class Hosting
    {
        Hosting(IWebHost webHost, IContainer container)
        {
            this.WebHost = webHost;
            this.Container = container;
        }

        public static Hosting Initialize(
            IConfigurationRoot configuration,
            X509Certificate2 serverCertificate,
            IDependencyManager dependencyManager)
        {
            int port = configuration.GetValue("httpSettings:port", 443);

            IWebHostBuilder webHostBuilder = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(!Socket.OSSupportsIPv6 ? IPAddress.Any : IPAddress.IPv6Any, port, listenOptions =>
                    {
                        listenOptions.UseHttps(serverCertificate, (connectionAdapterOptions) =>
                        {
                            connectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
                            connectionAdapterOptions.ClientCertificateValidation = (clientCert, chain, policyErrors) => true;
                        });
                    });
                })
                .UseSockets()
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddSingleton<IConfigurationRoot>(configuration);
                    serviceCollection.AddSingleton<IDependencyManager>(dependencyManager);
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
