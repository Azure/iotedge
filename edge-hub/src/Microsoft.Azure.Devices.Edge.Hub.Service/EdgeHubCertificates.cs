// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public class EdgeHubCertificates
    {
        EdgeHubCertificates(X509Certificate2 serverCertificate, IList<X509Certificate2> certificateChain, IList<X509Certificate2> trustBundle)
        {
            this.ServerCertificate = Preconditions.CheckNotNull(serverCertificate, nameof(serverCertificate));
            this.CertificateChain = Preconditions.CheckNotNull(certificateChain, nameof(certificateChain));
            this.TrustBundle = Preconditions.CheckNotNull(trustBundle, nameof(trustBundle));
        }

        public X509Certificate2 ServerCertificate { get; }

        public IList<X509Certificate2> CertificateChain { get; }

        public IList<X509Certificate2> TrustBundle { get; }

        public static async Task<EdgeHubCertificates> LoadAsync(IConfigurationRoot configuration)
        {
            Preconditions.CheckNotNull(configuration, nameof(configuration));
            EdgeHubCertificates result;
            string edgeHubDevCertPath = configuration.GetValue<string>(Constants.ConfigKey.EdgeHubDevServerCertificateFile);
            string edgeHubDevPrivateKeyPath = configuration.GetValue<string>(Constants.ConfigKey.EdgeHubDevServerPrivateKeyFile);
            string edgeHubDevTrustBundlePath = configuration.GetValue<string>(Constants.ConfigKey.EdgeHubDevTrustBundleFile);
            string edgeHubDockerCertPFXPath = configuration.GetValue<string>(Constants.ConfigKey.EdgeHubServerCertificateFile);
            string edgeHubDockerCaChainCertPath = configuration.GetValue<string>(Constants.ConfigKey.EdgeHubServerCAChainCertificateFile);
            string edgeHubConnectionString = configuration.GetValue<string>(Constants.ConfigKey.IotHubConnectionString);

            if (string.IsNullOrEmpty(edgeHubConnectionString))
            {
                // When connection string is not set it is edged mode as iotedgd is expected to set this.
                // In this case we reach out to the iotedged via the workload interface.
                (X509Certificate2 ServerCertificate, IEnumerable<X509Certificate2> CertificateChain) certificates;

                var workloadUri = new Uri(configuration.GetValue<string>(Constants.ConfigKey.WorkloadUri));
                string edgeHubHostname = configuration.GetValue<string>(Constants.ConfigKey.EdgeDeviceHostName);
                string moduleId = configuration.GetValue<string>(Constants.ConfigKey.ModuleId);
                string generationId = configuration.GetValue<string>(Constants.ConfigKey.ModuleGenerationId);
                string edgeletApiVersion = configuration.GetValue<string>(Constants.ConfigKey.WorkloadAPiVersion);
                DateTime expiration = DateTime.UtcNow.AddDays(Constants.CertificateValidityDays);

                certificates = await CertificateHelper.GetServerCertificatesFromEdgelet(workloadUri, edgeletApiVersion, Constants.WorkloadApiVersion, moduleId, generationId, edgeHubHostname, expiration);
                InstallCertificates(certificates.CertificateChain);
                IEnumerable<X509Certificate2> trustBundle = await CertificateHelper.GetTrustBundleFromEdgelet(workloadUri, edgeletApiVersion, Constants.WorkloadApiVersion, moduleId, generationId);

                result = new EdgeHubCertificates(
                    certificates.ServerCertificate,
                    certificates.CertificateChain?.ToList(),
                    trustBundle?.ToList());
            }
            else if (!string.IsNullOrEmpty(edgeHubDevCertPath) &&
                     !string.IsNullOrEmpty(edgeHubDevPrivateKeyPath) &&
                     !string.IsNullOrEmpty(edgeHubDevTrustBundlePath))
            {
                // If no connection string was set and we use iotedged workload style certificates for development
                (X509Certificate2 ServerCertificate, IEnumerable<X509Certificate2> CertificateChain) certificates;

                certificates = CertificateHelper.GetServerCertificateAndChainFromFile(edgeHubDevCertPath, edgeHubDevPrivateKeyPath);
                InstallCertificates(certificates.CertificateChain);

                IEnumerable<X509Certificate2> trustBundle = CertificateHelper.ParseTrustedBundleFromFile(edgeHubDevTrustBundlePath);

                result = new EdgeHubCertificates(
                    certificates.ServerCertificate,
                    certificates.CertificateChain?.ToList(),
                    trustBundle?.ToList());
            }
            else if (!string.IsNullOrEmpty(edgeHubDockerCertPFXPath) &&
                     !string.IsNullOrEmpty(edgeHubDockerCaChainCertPath))
            {
                // If no connection string was set and we use iotedge devdiv style certificates for development
                List<X509Certificate2> certificateChain = CertificateHelper.GetServerCACertificatesFromFile(edgeHubDockerCaChainCertPath)?.ToList();
                InstallCertificates(certificateChain);
                result = new EdgeHubCertificates(new X509Certificate2(edgeHubDockerCertPFXPath), certificateChain, new List<X509Certificate2>());
            }
            else
            {
                throw new InvalidOperationException("Edge Hub certificate files incorrectly configured");
            }

            return result;
        }

        static void InstallCertificates(IEnumerable<X509Certificate2> certificateChain)
        {
            string message;
            if (certificateChain != null)
            {
                X509Certificate2[] certs = certificateChain.ToArray();
                message = $"Found intermediate certificates: {string.Join(",", certs.Select(c => $"[{c.Subject}:{c.GetExpirationDateString()}]"))}";

                CertificateHelper.InstallCerts(
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StoreName.CertificateAuthority : StoreName.Root,
                    StoreLocation.CurrentUser,
                    certs);
            }
            else
            {
                message = "Unable to find intermediate certificates.";
            }

            Console.WriteLine($"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)}] {message}");
        }
    }
}
