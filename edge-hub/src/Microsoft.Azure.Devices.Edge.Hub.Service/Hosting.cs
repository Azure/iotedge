// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using Autofac;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public class Hosting
    {
        Hosting(IWebHost webHost, IContainer container)
        {
            this.WebHost = webHost;
            this.Container = container;
        }

        public IContainer Container { get; }

        public IWebHost WebHost { get; }

        public static Hosting Initialize(
            IConfigurationRoot configuration,
            X509Certificate2 serverCertificate,
            IDependencyManager dependencyManager,
            bool clientCertAuthEnabled,
            SslProtocols sslProtocols)
        {
            int port = configuration.GetValue("httpSettings:port", 443);
            IWebHostBuilder webHostBuilder = new WebHostBuilder()
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
                                        OnAuthenticate = (context, options) =>
                                        {
                                            if (clientCertAuthEnabled)
                                            {
                                                options.ClientCertificateRequired = true;
                                                options.RemoteCertificateValidationCallback = (_, clientCert, chain, policyErrors) =>
                                                {
                                                    TlsConnectionFeatureExtended tlsConnectionFeatureExtended = GetConnectionFeatureExtended(Option.Some(chain));
                                                    context.Features.Set<ITlsConnectionFeatureExtended>(tlsConnectionFeatureExtended);
                                                    return true;
                                                };
                                            }
                                        },
                                        ClientCertificateMode = ClientCertificateMode.NoCertificate, // we can override this in OnAuthenticate if using certs
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
                .UseStartup<Startup>();
            IWebHost webHost = webHostBuilder.Build();
            IContainer container = webHost.Services.GetService(typeof(IStartup)) is Startup startup ? startup.Container : null;
            return new Hosting(webHost, container);
        }

        public static TlsConnectionFeatureExtended GetConnectionFeatureExtended(Option<X509Chain> chain)
        {
            IList<X509Certificate2> clientCertChain = new List<X509Certificate2>();
            chain.ForEach(chain =>
            {
                foreach (X509ChainElement chainElement in chain.ChainElements)
                {
                    clientCertChain.Add(new X509Certificate2(chainElement.Certificate));
                }
            });

            return new TlsConnectionFeatureExtended
            {
                ChainElements = clientCertChain
            };
        }
    }
}
