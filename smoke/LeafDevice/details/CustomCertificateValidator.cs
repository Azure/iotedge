// Copyright (c) Microsoft. All rights reserved.
namespace LeafDeviceTest
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Common;

    // NOTE: this class is copied from Azure IoT C# SDK
    class CustomCertificateValidator
    {
        readonly IEnumerable<X509Certificate2> certs;
        readonly ITransportSettings[] transportSettings;

        CustomCertificateValidator(IList<X509Certificate2> certs, ITransportSettings[] transportSettings)
        {
            this.certs = certs;
            this.transportSettings = transportSettings;
        }

        public static CustomCertificateValidator Create(
            IList<X509Certificate2> certs,
            ITransportSettings[] transportSettings)
        {
            var instance = new CustomCertificateValidator(certs, transportSettings);
            instance.SetupCertificateValidation();
            return instance;
        }

        public Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> GetCustomCertificateValidation()
        {
            Debug.WriteLine("CustomCertificateValidator.GetCustomCertificateValidation()");

            return (sender, cert, chain, sslPolicyErrors) => ValidateCertificate(this.certs.First(), cert, chain, sslPolicyErrors);
        }

        void SetupCertificateValidation()
        {
            Debug.WriteLine("CustomCertificateValidator.SetupCertificateValidation()");

            foreach (ITransportSettings transportSetting in this.transportSettings)
            {
                switch (transportSetting.GetTransportType())
                {
                    case TransportType.Amqp_WebSocket_Only:
                    case TransportType.Amqp_Tcp_Only:
                        if (transportSetting is AmqpTransportSettings amqpTransportSettings)
                        {
                            if (amqpTransportSettings.RemoteCertificateValidationCallback == null)
                            {
                                amqpTransportSettings.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => ValidateCertificate(this.certs.First(), certificate, chain, sslPolicyErrors);
                            }
                        }

                        break;
                    case TransportType.Http1:
                        // InvokeMethodAsync is over HTTP even when transportSettings set a different protocol
                        // So set the callback in HttpClientHandler for InvokeMethodAsync
                        break;
                    case TransportType.Mqtt_WebSocket_Only:
                    case TransportType.Mqtt_Tcp_Only:
                        if (transportSetting is MqttTransportSettings mqttTransportSettings)
                        {
                            if (mqttTransportSettings.RemoteCertificateValidationCallback == null)
                            {
                                mqttTransportSettings.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => ValidateCertificate(this.certs.First(), certificate, chain, sslPolicyErrors);
                            }
                        }

                        break;
                    default:
                        throw new InvalidOperationException("Unsupported Transport Type {0}".FormatInvariant(transportSetting.GetTransportType()));
                }
            }
        }

        static bool ValidateCertificate(X509Certificate2 trustedCertificate, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            Console.WriteLine("CustomCertificateValidator.ValidateCertificate is called.");
            // Terminate on errors other than those caused by a chain failure
            SslPolicyErrors terminatingErrors = sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors;
            if (terminatingErrors != SslPolicyErrors.None)
            {
                Console.WriteLine("Discovered SSL session errors: {0}", terminatingErrors);
                return false;
            }

            // Allow the chain the chance to rebuild itself with the expected root
            chain.ChainPolicy.ExtraStore.Add(trustedCertificate);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
#if NETSTANDARD2_0
            if (!chain.Build(new X509Certificate2(certificate)))
            {
                Console.WriteLine("Unable to build the chain using the expected root certificate.");
                return false;
            }
#else
            if (!chain.Build(new X509Certificate2(certificate.Export(X509ContentType.Cert))))
            {
                Console.WriteLine("Unable to build the chain using the expected root certificate.");
                return false;
            }
#endif

            // Pin the trusted root of the chain to the expected root certificate
            X509Certificate2 actualRoot = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
            if (!trustedCertificate.Equals(actualRoot))
            {
                Console.WriteLine("The certificate chain was not signed by the trusted root certificate.");
                return false;
            }

            Console.WriteLine("CustomCertificateValidator.ValidateCertificate is passed.");
            return true;
        }
    }
}
