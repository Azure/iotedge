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
    using Microsoft.Azure.Devices.Edge.Hub.Http.Controllers;
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
            /* Define the HttpsConnectionAdapterOptions which integrates with aspnetcore https middleware to help us manage the client cert chain.
               We need to store the client cert chain in the connection context, so that we can perform custom CA validation later to authenticate with the service identity.
               We are using logic here that depends on an undocumented api, however eliminating this dependency would require a large refactor.
               Context in this issue: https://github.com/dotnet/aspnetcore/issues/21606
               Link to aspnet https middleware: https://github.com/dotnet/aspnetcore/blob/e81033e094d4663ffd227bb4aed30b76b0631e6d/src/Servers/Kestrel/Core/src/Middleware/HttpsConnectionMiddleware.cs */
            HttpsConnectionAdapterOptions connectionAdapterOptions = new HttpsConnectionAdapterOptions()
            {
                /* ClientCertificateMode.AllowCertificate is the intuitive choice here, however is not what we need
                   If we use this then aspnetcore will register it's own certificate validation callback when creating the SslStream.
                   This aspnetcore-created certificate validation callback in turn calls the HttpsConnectionAdapterOptions.ClientCertificateValidation, which we can configure In HttpsConnectionAdapterOptions.
                   This is not the desired solution for us because HttpsConnectionAdapterOptions.ClientCertificateValidation does not provide access to the client cert chain when the connection is in scope.

                   Thus, we need to pass NoCertificate to block aspnet from creating the SslStream with the certificate validation callback.
                   This will let us use HttpsConnectionAdapterOptions.OnAuthenticate in order to register certificate validation with the connection in scope.
                   We can handle the cert / no-cert case inside of OnAuthenticate */
                ClientCertificateMode = ClientCertificateMode.NoCertificate,
                ServerCertificate = serverCertificate,
                SslProtocols = sslProtocols,
                OnAuthenticate = (context, options) =>
                {
                    if (clientCertAuthEnabled)
                    {
                        /* Certificate authorization can be enabled in edgeHub, however direct methods will hit this logic and not need certificates.
                           This implies that the ClientCertificateRequired should be false.
                           However, the naming of this field is not semantically correct.
                           We looked at how aspnetcore used this field, and found that it is true when CertificateMode.AllowCertificate is set (which does not require certificates).
                           Therefore we can set this field to true and it will still work with direct methods.
                           */
                        options.ClientCertificateRequired = true;
                        options.RemoteCertificateValidationCallback = (_, clientCert, chain, policyErrors) =>
                        {
                            Option<X509Chain> chainOption = Option.Maybe(chain);
                            TlsConnectionFeatureExtended tlsConnectionFeatureExtended = GetConnectionFeatureExtended(chainOption);
                            context.Features.Set<ITlsConnectionFeatureExtended>(tlsConnectionFeatureExtended);
                            return true;
                        };
                    }
                }
            };

            int mainPort = configuration.GetValue("httpSettings:port", 443);
            int metricsPort = configuration.GetValue("httpSettings:metrics_port", 9600);
            IWebHostBuilder webHostBuilder = new WebHostBuilder()
                .UseKestrel(
                    options =>
                    {
                        options.Listen(
                            !Socket.OSSupportsIPv6 ? IPAddress.Any : IPAddress.IPv6Any,
                            mainPort,
                            listenOptions =>
                            {
                                listenOptions.UseHttps(connectionAdapterOptions);
                            });

                        options.Listen(!Socket.OSSupportsIPv6 ? IPAddress.Any : IPAddress.IPv6Any, metricsPort);
                    })
                .UseSockets()
                .ConfigureServices(
                    serviceCollection =>
                    {
                        serviceCollection.AddSingleton(configuration);
                        serviceCollection.AddSingleton(dependencyManager);
                    })
                .UseStartup<Startup>()
                .UseSetting(WebHostDefaults.ApplicationKey, typeof(TwinsController).Assembly.GetName().Name);
            IWebHost webHost = webHostBuilder.Build();
            IContainer container = webHost.Services.GetService(typeof(IStartup)) is Startup startup ? startup.Container : null;
            return new Hosting(webHost, container);
        }

        // This function wraps the client cert chain in special object for later storage in the connection context.
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
