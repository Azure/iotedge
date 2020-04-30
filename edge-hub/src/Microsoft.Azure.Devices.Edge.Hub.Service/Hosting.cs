// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using Autofac;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public class Hosting
    {
        Hosting(IHost host, IContainer container)
        {
            this.Host = host;
            this.Container = container;
        }

        public IContainer Container { get; }

        public IHost Host { get; }

        public static Hosting Initialize(
            IConfigurationRoot configuration,
            X509Certificate2 serverCertificate,
            IDependencyManager dependencyManager,
            bool clientCertAuthEnabled,
            SslProtocols sslProtocols)
        {
            int port = configuration.GetValue("httpSettings:port", 443);
            var certificateMode = clientCertAuthEnabled ? ClientCertificateMode.AllowCertificate : ClientCertificateMode.NoCertificate;

            IHost host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseKestrel(
                            options =>
                            {
                                options.Listen(
                                    !Socket.OSSupportsIPv6 ? IPAddress.Any : IPAddress.IPv6Any,
                                    port,
                                    listenOptions =>
                                    {
                                        listenOptions.UseHttps(
                                            new HttpsConnectionAdapterOptions()
                                            {
                                                ServerCertificate = serverCertificate,
                                                ClientCertificateValidation = (clientCert, chain, policyErrors) => true,
                                                ClientCertificateMode = certificateMode,
                                                SslProtocols = sslProtocols
                                            });
                                    });
                            })
                        .UseSockets()
                        .ConfigureServices(
                            serviceCollection =>
                            {
                                serviceCollection.AddSingleton(configuration);
                                serviceCollection.AddSingleton(dependencyManager);
                            })
                        .UseStartup<Startup2>();
                }).Build();

            IContainer container = host.Services.GetService(typeof(Startup2)) is Startup2 startup ? startup.Container : null;
            return new Hosting(host, container);
        }
    }
}
