// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.CertificateHelper
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Extensions.Logging;

    public static class CertificateHelper
    {
        public static string GetSHA256Thumbprint(X509Certificate2 cert)
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
                bool chainBuildSucceeded = chain.Build(cert as X509Certificate2 ?? new X509Certificate2(cert.Export(X509ContentType.Cert)));
                X509ChainStatusFlags flags = X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.PartialChain;
                IEnumerable<X509ChainStatus> filteredStatus = chain.ChainStatus.Where(cs => !flags.HasFlag(cs.Status));
                if (!chainBuildSucceeded || filteredStatus.Any())
                {
                    string errors = $"Certificate with subject: {cert.Subject} failed with errors: ";
                    string s = filteredStatus
                        .Select(c => c.StatusInformation)
                        .Aggregate(errors, (prev, curr) => $"{prev} {curr}");
                    return (new List<X509Certificate2>(), Option.Some(errors));
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
                string thumbprint = GetSHA256Thumbprint(chainElement);
                if (remoteCertificateChain.Any(cert => GetSHA256Thumbprint(cert) == thumbprint) &&
                    caChainCerts.Any(cert => GetSHA256Thumbprint(cert) == thumbprint))
                {
                    match = true;
                    break;
                }
            }

            if (!match)
            {
                return (false, Option.Some($"Error validating cert with Subject: {remoteCertificate.SubjectName} Thumbprint: {GetSHA256Thumbprint(remoteCertificate)}"));
            }

            return (match, Option.None<string>());
        }

        public static bool ValidateClientCert(X509Certificate certificate, X509Chain chain, Option<IList<X509Certificate2>> caChainCerts, ILogger logger)
        {
            Preconditions.CheckNotNull(certificate);
            Preconditions.CheckNotNull(chain);
            Preconditions.CheckNotNull(logger);

            X509Certificate2 newCert = new X509Certificate2(certificate);

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

            X509Certificate2 cert = new X509Certificate2(X509Certificate.CreateFromCertFile(path));
            (IList<X509Certificate2> chainCerts, Option<string> errors) = BuildCertificateList(cert);
            if (errors.HasValue)
            {
                return (Option.None<IList<X509Certificate2>>(), errors);
            }
            return (Option.Some(chainCerts), Option.None<string>());
        }

        public static void InstallCerts(StoreName name, StoreLocation location, IEnumerable<X509Certificate2> certs)
        {
            using (X509Store store = new X509Store(name, location))
            {
                store.Open(OpenFlags.ReadWrite);
                foreach (var cert in certs)
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

            using (StreamReader sr = new StreamReader(certPath))
            {
                // Extract each certificate's string. The final string from the split will either be empty
                // or a non-certificate entry, so it is dropped.
                string delimiter = @"-----END CERTIFICATE-----";
                string[] rawCerts = sr.ReadToEnd().Split(new []{delimiter}, StringSplitOptions.None);
                return rawCerts
                    .Take(rawCerts.Count() - 1) // Drop the invalid entry
                    .Select(c => $"{c}\r\n{delimiter}") // Re-add the certificate end-marker which was removed by split
                    .Select(c => System.Text.Encoding.UTF8.GetBytes(c))
                    .Select(c => new X509Certificate2(c));
            }
        }
    }
}
