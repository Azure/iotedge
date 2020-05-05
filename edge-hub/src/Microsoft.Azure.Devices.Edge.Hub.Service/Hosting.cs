// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using Autofac;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;

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
            var certificateMode = clientCertAuthEnabled ? ClientCertificateMode.AllowCertificate : ClientCertificateMode.NoCertificate;
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
                                        ClientCertificateValidation = (clientCert, chain, policyErrors) => // TODO: Store cert and add to context in startup. Inject TlsFeatures into Startup class. Eliminate hacky POC.
                                        {
                                            Console.WriteLine("---------------Setting map entry for tls feature----------------------");
                                            IList<X509Certificate2> chainElements = new List<X509Certificate2>();
                                            foreach (X509ChainElement element in chain.ChainElements)
                                            {
                                                chainElements.Add(element.Certificate);
                                            }

                                            Console.WriteLine($"---------------------{JsonConvert.SerializeObject(chainElements)}-----------------------");
                                            Console.WriteLine($"---------------------------{clientCert.Thumbprint}----------------------------------");

                                            CertContext.CertsToChain.Add(clientCert, new TlsConnectionFeatureExtended
                                            {
                                                ChainElements = chainElements
                                            });

                                            Console.WriteLine($"---------------Count of chain elements: {chainElements.Count}----------------------");

                                            return true;
                                        },
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
                .UseStartup<Startup>();
            IWebHost webHost = webHostBuilder.Build();
            IContainer container = webHost.Services.GetService(typeof(IStartup)) is Startup startup ? startup.Container : null;
            return new Hosting(webHost, container);
        }

        // static void ClientCertificateValidation(X509Certificate2 clientCert, X509Chain chain, SslPolicyErrors policyErrors)
        // {

        // }
    }
}
