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
    using Microsoft.Extensions.Logging;

    public class EdgeHubCertificates
    {
        IList<X509Certificate2> certificateChain;

        public X509Certificate2 ServerCertificate { get; private set; }
        
        EdgeHubCertificates(X509Certificate2 serverCertificate, IList<X509Certificate2> certificateChain)
        {
            this.ServerCertificate = serverCertificate;
            this.certificateChain = certificateChain;

            this.LogStatus();
        }
        
        public static async Task<EdgeHubCertificates> LoadAsync(IConfigurationRoot configuration)
        {
            Preconditions.CheckNotNull(configuration, nameof(configuration));

            string edgeHubConnectionString = configuration.GetValue<string>(Constants.ConfigKey.IotHubConnectionString);

            // When connection string is not set it is edged mode
            if (string.IsNullOrEmpty(edgeHubConnectionString))
            {
                var workloadUri = new Uri(configuration.GetValue<string>(Constants.ConfigKey.WorkloadUri));
                string edgeHubHostname = configuration.GetValue<string>(Constants.ConfigKey.EdgeDeviceHostName);
                string moduleId = configuration.GetValue<string>(Constants.ConfigKey.ModuleId);
                string generationId = configuration.GetValue<string>(Constants.ConfigKey.ModuleGenerationId);
                DateTime expiration = DateTime.UtcNow.AddDays(Constants.CertificateValidityDays);
                (X509Certificate2 ServerCertificate, IEnumerable<X509Certificate2> CertificateChain) certificates =
                    await CertificateHelper.GetServerCertificatesFromEdgelet(workloadUri, Constants.WorkloadApiVersion, moduleId, generationId, edgeHubHostname, expiration);

                InstallCertificates(certificates.CertificateChain);
                return new EdgeHubCertificates(certificates.ServerCertificate, certificates.CertificateChain?.ToList());
            }
            
            string edgeHubCertPath = configuration.GetValue<string>(Constants.ConfigKey.EdgeHubServerCertificateFile);
            string edgeHubCaChainCertPath = configuration.GetValue<string>(Constants.ConfigKey.EdgeHubServerCAChainCertificateFile);
            List<X509Certificate2> certificateChain = CertificateHelper.GetServerCACertificatesFromFile(edgeHubCaChainCertPath)?.ToList();

            InstallCertificates(certificateChain);
            return new EdgeHubCertificates(new X509Certificate2(edgeHubCertPath), certificateChain);
        }

        static void InstallCertificates(IEnumerable<X509Certificate2> certificateChain)
        {
            if (certificateChain != null)
            {
                CertificateHelper.InstallCerts(
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StoreName.CertificateAuthority : StoreName.Root,
                    StoreLocation.CurrentUser,
                    certificateChain);
            }
        }

        void LogStatus()
        {
            string message = this.certificateChain == null ? "Unable to find intermediate certificates." : "Found intermediate certificates.";
            Console.WriteLine($"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)}] {message}");
        }
    }
}
