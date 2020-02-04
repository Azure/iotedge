// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Client.Samples
{
    using System;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;

    static class CustomCertificateValidator
    {
        public static bool ValidateCertificate(X509Certificate2 trustedCertificate, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Terminate on errors other than those caused by a chain failure
            SslPolicyErrors terminatingErrors = sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors;
            if (terminatingErrors != SslPolicyErrors.None)
            {
                Console.WriteLine("Discovered SSL session errors: {0}", terminatingErrors);
                return false;
            }

            // Allow the chain the chance to rebuild itself with the expected root
            chain.ChainPolicy.ExtraStore.Add(trustedCertificate);
            // Edge test certificates have no revocation support so explicitly setting revocation mode to NoCheck. Otherwise test with AMQP will fail for certificate validation.
            // Note: we don't know why revocation mode is NoCheck when using MQTT.
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
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

            return true;
        }
    }
}
