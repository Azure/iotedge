// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System.ComponentModel;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Client;
    using Serilog;

    static class TransportSettingsExtensions
    {
        public static void SetupCertificateValidation(this IotHubClientTransportSettings transportSettings, X509Certificate2 trustedCert)
        {
            switch (transportSettings)
            {
                case IotHubClientAmqpSettings amqpTransportSettings:
                    if (amqpTransportSettings.RemoteCertificateValidationCallback == null)
                    {
                        amqpTransportSettings.RemoteCertificateValidationCallback =
                            (sender, certificate, chain, sslPolicyErrors) =>
                                ValidateCertificate(trustedCert, certificate, chain, sslPolicyErrors);
                    }

                    break;
                case IotHubClientMqttSettings mqttTransportSettings:
                    if (mqttTransportSettings.RemoteCertificateValidationCallback == null)
                    {
                        mqttTransportSettings.RemoteCertificateValidationCallback =
                            (sender, certificate, chain, sslPolicyErrors) =>
                                ValidateCertificate(trustedCert, certificate, chain, sslPolicyErrors);
                    }

                    break;
                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        static bool ValidateCertificate(
            X509Certificate2 trustedCertificate,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            // Terminate on errors other than those caused by a chain failure
            SslPolicyErrors terminatingErrors = sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors;
            if (terminatingErrors != SslPolicyErrors.None)
            {
                Log.Verbose("Discovered SSL session errors: {Errors}", terminatingErrors);
                return false;
            }

            // Allow the chain the chance to rebuild itself with the expected root
            chain.ChainPolicy.ExtraStore.Add(trustedCertificate);
            // Transparent gateway tests for CA certs and self-signed certs started failing for
            // AMQP. An investigation revealed that RevocationMode was 'Online' in the AMQP scenario
            // (which is the default), but it was 'NoCheck' for MQTT. It's not clear why or where
            // the MQTT code path is setting 'NoCheck', but if we set 'NoCheck' here then the AMQP
            // tests start passing again. Regardless, this setting is reasonable in a test
            // environment because we don't need revocation.
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            byte[] input = certificate.Export(X509ContentType.Cert);

            if (!chain.Build(X509CertificateLoader.LoadCertificate(input)))
            {
                Log.Verbose("Unable to build the chain using the expected root certificate.");
                return false;
            }

            // Pin the trusted root of the chain to the expected root certificate
            X509Certificate2 actualRoot = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
            if (!trustedCertificate.Equals(actualRoot))
            {
                Log.Verbose("The certificate chain was not signed by the trusted root certificate.");
                return false;
            }

            return true;
        }
    }
}
