namespace Microsoft.Azure.Devices.Client.Samples
{

    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System;

    static class CustomCertificateValidator
    {
        public static bool ValidateCertificate(X509Certificate2 trustedCertificate, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
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
            // Edge test certificates have no revocation support so explicitly setting revocation mode to NoCheck. Otherwise test with AMQP will fail for certificate validation.
            // Note: we don't know why revocation mode is NoCheck when using MQTT.
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

            Console.WriteLine("*****************************************************************");
            Console.WriteLine($"Chain Information: {chain.ChainElements.Count}");
            Console.WriteLine("Chain revocation flag: {0}", chain.ChainPolicy.RevocationFlag);
            Console.WriteLine("Chain revocation mode: {0}", chain.ChainPolicy.RevocationMode);
            Console.WriteLine("Chain verification flag: {0}", chain.ChainPolicy.VerificationFlags);
            Console.WriteLine("Chain verification time: {0}", chain.ChainPolicy.VerificationTime);
            Console.WriteLine("Chain status length: {0}", chain.ChainStatus.Length);
            Console.WriteLine("Chain application policy count: {0}", chain.ChainPolicy.ApplicationPolicy.Count);
            Console.WriteLine("Chain certificate policy count: {0} {1}", chain.ChainPolicy.CertificatePolicy.Count, Environment.NewLine);

            // Output chain element information.
            Console.WriteLine("*****************************************************************");
            Console.WriteLine("Chain Element Information");
            Console.WriteLine("Number of chain elements: {0}", chain.ChainElements.Count);
            Console.WriteLine("Chain elements synchronized? {0} {1}", chain.ChainElements.IsSynchronized, Environment.NewLine);

            foreach (X509ChainElement element in chain.ChainElements)
            {
                Console.WriteLine("Element issuer name: {0}", element.Certificate.Issuer);
                Console.WriteLine("Element certificate valid until: {0}", element.Certificate.NotAfter);
                Console.WriteLine("Element certificate is valid: {0}", element.Certificate.Verify());
                Console.WriteLine("Element error status length: {0}", element.ChainElementStatus.Length);
                Console.WriteLine("Element information: {0}", element.Information);
                Console.WriteLine("Number of element extensions: {0}{1}", element.Certificate.Extensions.Count, Environment.NewLine);

                if (chain.ChainStatus.Length > 1)
                {
                    for (int index = 0; index < element.ChainElementStatus.Length; index++)
                    {
                        Console.WriteLine(element.ChainElementStatus[index].Status);
                        Console.WriteLine(element.ChainElementStatus[index].StatusInformation);
                    }
                }
            }

            Console.WriteLine("*****************************************************************");
            Console.WriteLine("Trusted Certificate:");
            Console.WriteLine($"    FriendlyName = {trustedCertificate.FriendlyName}");
            Console.WriteLine($"    IssuerName = {trustedCertificate.IssuerName.Name}");
            Console.WriteLine($"    SubjectName = {trustedCertificate.SubjectName.Name}");

            Console.WriteLine("*****************************************************************");
            Console.WriteLine("Certificate:");
            Console.WriteLine($"    IssuerName = {certificate.Issuer}");
            Console.WriteLine($"    SubjectName = {certificate.Subject}");

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