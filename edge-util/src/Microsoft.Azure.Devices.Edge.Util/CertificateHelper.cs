// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.Edged.GeneratedCode;
    using Microsoft.Extensions.Logging;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.OpenSsl;
    using Org.BouncyCastle.Pkcs;
    using Org.BouncyCastle.Security;

    public static class CertificateHelper
    {
        public static string GetSha256Thumbprint(X509Certificate2 cert)
        {
            Preconditions.CheckNotNull(cert);
            using (var sha256 = new SHA256Managed())
            {
                byte[] hash = sha256.ComputeHash(cert.RawData);
                return ToHexString(hash);
            }
        }

        static string ToHexString(byte[] bytes)
        {
            Preconditions.CheckNotNull(bytes);
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        public static (IList<X509Certificate2>, Option<string>) BuildCertificateList(X509Certificate2 cert)
        {
            var chain = new X509Chain
            {
                ChainPolicy =
                {
                    //For performance reasons do not check revocation status.
                    RevocationMode = X509RevocationMode.NoCheck,
                    //Does not check revocation status of the root certificate (sounds like it is meaningless with the option above - ask Simon or Alex)
                    RevocationFlag = X509RevocationFlag.ExcludeRoot,
                    //Certificate Authority can be unknown if it is not issued directly by a well-known CA
                    VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority
                }
            };

            try
            {
                bool chainBuildSucceeded = chain.Build(cert);
                X509ChainStatusFlags flags = X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.PartialChain;
                List<X509ChainStatus> filteredStatus = chain.ChainStatus.Where(cs => !flags.HasFlag(cs.Status)).ToList();
                if (!chainBuildSucceeded || filteredStatus.Count > 0)
                {
                    string errors = $"Certificate with subject: {cert.Subject} failed with errors: ";
                    string s = filteredStatus
                        .Select(c => c.StatusInformation)
                        .Aggregate(errors, (prev, curr) => $"{prev} {curr}");
                    return (new List<X509Certificate2>(), Option.Some(s));
                }

                IList<X509Certificate2> chainElements = GetCertificatesFromChain(chain);
                return (chainElements, Option.None<string>());
            }
            finally
            {
                chain.Reset();
            }
        }

        public static IList<X509Certificate2> GetCertificatesFromChain(X509Chain chainCert) =>
            chainCert.ChainElements.Cast<X509ChainElement>().Select(element => element.Certificate).ToList();

        public static (bool, Option<string>) ValidateCert(X509Certificate2 remoteCertificate, IList<X509Certificate2> remoteCertificateChain, IList<X509Certificate2> caChainCerts)
        {
            Preconditions.CheckNotNull(remoteCertificate);
            Preconditions.CheckNotNull(remoteCertificateChain);
            Preconditions.CheckNotNull(caChainCerts);

            bool match = false;

            (IList<X509Certificate2> remoteCerts, Option<string> errors) = BuildCertificateList(remoteCertificate);
            if (errors.HasValue)
            {
                return (false, errors);
            }
            foreach (X509Certificate2 chainElement in remoteCerts)
            {
                string thumbprint = GetSha256Thumbprint(chainElement);
                if (remoteCertificateChain.Any(cert => GetSha256Thumbprint(cert) == thumbprint) &&
                    caChainCerts.Any(cert => GetSha256Thumbprint(cert) == thumbprint))
                {
                    match = true;
                    break;
                }
            }

            if (!match)
            {
                return (false, Option.Some($"Error validating cert with Subject: {remoteCertificate.SubjectName} Thumbprint: {GetSha256Thumbprint(remoteCertificate)}"));
            }
            else
            {
                return (true, Option.None<string>());
            }
        }

        public static bool ValidateClientCert(X509Certificate certificate, X509Chain chain, Option<IList<X509Certificate2>> caChainCerts, ILogger logger)
        {
            Preconditions.CheckNotNull(certificate);
            Preconditions.CheckNotNull(chain);
            Preconditions.CheckNotNull(logger);

            var newCert = new X509Certificate2(certificate);

            if (!caChainCerts.HasValue)
            {
                logger.LogWarning($"Cannot validate certificate with subject: {certificate.Subject}. Hub CA cert chain missing.");
                return false;
            }

            DateTime currentTime = DateTime.Now;
            if (newCert.NotAfter < currentTime)
            {
                logger.LogWarning($"Certificate with subject: {newCert.Subject} has expired");
                return false;
            }

            if (newCert.NotBefore > currentTime)
            {
                logger.LogWarning($"Certificate with subject: {newCert.Subject} is not valid");
                return false;
            }

            IList<X509Certificate2> certChain = GetCertificatesFromChain(chain);

            bool result = false;
            Option<string> errors = Option.None<string>();
            caChainCerts.ForEach(v => { (result, errors) = ValidateCert(newCert, certChain, v); });
            if (errors.HasValue)
            {
                errors.ForEach(v => logger.LogWarning(v));
            }

            return result;
        }

        public static (Option<IList<X509Certificate2>>, Option<string>) GetCertsAtPath(string path)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));

            var cert = new X509Certificate2(X509Certificate.CreateFromCertFile(path));
            (IList<X509Certificate2> chainCerts, Option<string> errors) = BuildCertificateList(cert);
            if (errors.HasValue)
            {
                return (Option.None<IList<X509Certificate2>>(), errors);
            }
            return (Option.Some(chainCerts), Option.None<string>());
        }

        public static void InstallCerts(StoreName name, StoreLocation location, IEnumerable<X509Certificate2> certs)
        {
            List<X509Certificate2> certsList = Preconditions.CheckNotNull(certs, nameof(certs)).ToList();
            using (var store = new X509Store(name, location))
            {
                store.Open(OpenFlags.ReadWrite);
                foreach (X509Certificate2 cert in certsList)
                {
                    store.Add(cert);
                }
            }
        }

        public static IEnumerable<X509Certificate2> ExtractCertsFromPem(string certPath)
        {
            if (string.IsNullOrWhiteSpace(certPath) || !File.Exists(certPath))
            {
                throw new ArgumentException($"'{certPath}' is not a path to a certificate collection file");
            }

            using (var sr = new StreamReader(certPath))
            {
                return GetCertificatesFromPem(ParsePemCerts(sr.ReadToEnd()));
            }
        }

        public static IEnumerable<X509Certificate2> GetCertificatesFromPem(IEnumerable<string> rawPemCerts)
        {
            return rawPemCerts
                .Select(c => System.Text.Encoding.UTF8.GetBytes(c))
                .Select(c => new X509Certificate2(c))
                .ToList();
        }

        public static async Task<(X509Certificate2 ServerCertificate, IEnumerable<X509Certificate2> CertificateChain)> GetServerCertificatesFromEdgelet(Uri workloadUri, string workloadApiVersion, string moduleId, string moduleGenerationId, string edgeHubHostname, DateTime expiration)
        {
            if (string.IsNullOrEmpty(edgeHubHostname))
            {
                throw new InvalidOperationException($"{nameof(edgeHubHostname)} is required.");
            }

            CertificateResponse response = await new WorkloadClient(workloadUri, workloadApiVersion, moduleId, moduleGenerationId).CreateServerCertificateAsync(edgeHubHostname, expiration);
            return ParseCertificateResponse(response);
        }

        public static IEnumerable<X509Certificate2> GetServerCACertificatesFromFile(string chainPath)
        {
            IEnumerable<X509Certificate2> certChain = !string.IsNullOrWhiteSpace(chainPath) ? ExtractCertsFromPem(chainPath) : null;
            return certChain;
        }

        public static IList<string> ParsePemCerts(string pemCerts)
        {
            if (string.IsNullOrEmpty(pemCerts))
            {
                throw new InvalidOperationException("Trusted certificates can not be null or empty.");
            }

            // Extract each certificate's string. The final string from the split will either be empty
            // or a non-certificate entry, so it is dropped.
            string delimiter = "-----END CERTIFICATE-----";
            string[] rawCerts = pemCerts.Split(new[] { delimiter }, StringSplitOptions.None);
            return rawCerts
                .Take(rawCerts.Count() - 1) // Drop the invalid entry
                .Select(c => $"{c}{delimiter}")
                .ToList(); // Re-add the certificate end-marker which was removed by split
        }

        internal static (X509Certificate2, IEnumerable<X509Certificate2>) ParseCertificateResponse(CertificateResponse response)
        {
            IEnumerable<string> pemCerts = ParsePemCerts(response.Certificate);

            if (pemCerts.FirstOrDefault() == null)
            {
                throw new InvalidOperationException("Certificate is required");
            }

            IEnumerable<X509Certificate2> certsChain = GetCertificatesFromPem(pemCerts.Skip(1));

            Pkcs12Store store = new Pkcs12StoreBuilder().Build();
            IList<X509CertificateEntry> chain = new List<X509CertificateEntry>();

            var sr = new StringReader(pemCerts.First() + "\r\n" + response.PrivateKey.Bytes);
            var pemReader = new PemReader(sr);

            RsaPrivateCrtKeyParameters keyParams = null;
            object certObject = pemReader.ReadObject();
            while (certObject != null)
            {
                if (certObject is Org.BouncyCastle.X509.X509Certificate x509Cert)
                {
                    chain.Add(new X509CertificateEntry(x509Cert));
                }
                if (certObject is RsaPrivateCrtKeyParameters)
                {
                    keyParams = ((RsaPrivateCrtKeyParameters)certObject);
                }

                certObject = pemReader.ReadObject();
            }

            if (keyParams == null)
            {
                throw new InvalidOperationException("Private key is required");
            }

            store.SetKeyEntry("Edge", new AsymmetricKeyEntry(keyParams), chain.ToArray());
            using (var p12File = new MemoryStream())
            {
                store.Save(p12File, new char[] { }, new SecureRandom());

                var cert = new X509Certificate2(p12File.ToArray());
                return (cert, certsChain);
            }
        }
    }
}
