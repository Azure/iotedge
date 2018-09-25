namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class EdgeHubCertificates
    {
        readonly IConfigurationRoot configuration;
        (X509Certificate2 ServerCertificate, IList<X509Certificate2> CertificateChain) serverCertificates;
        bool isLoaded = false;

        public EdgeHubCertificates(IConfigurationRoot configuration)
        {
            this.configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
        }

        public X509Certificate2 ServerCertificate => this.serverCertificates.ServerCertificate;
        
        public async Task LoadAsync()
        {
            string edgeHubConnectionString = this.configuration.GetValue<string>(Constants.ConfigKey.IotHubConnectionString);

            // When connection string is not set it is edged mode
            if (string.IsNullOrEmpty(edgeHubConnectionString))
            {
                var workloadUri = new Uri(this.configuration.GetValue<string>(Constants.ConfigKey.WorkloadUri));
                string edgeHubHostname = this.configuration.GetValue<string>(Constants.ConfigKey.EdgeDeviceHostName);
                string moduleId = this.configuration.GetValue<string>(Constants.ConfigKey.ModuleId);
                string generationId = this.configuration.GetValue<string>(Constants.ConfigKey.ModuleGenerationId);
                DateTime expiration = DateTime.UtcNow.AddDays(Constants.CertificateValidityDays);
                (X509Certificate2 ServerCertificate, IEnumerable<X509Certificate2> CertificateChain) certificates =
                    await CertificateHelper.GetServerCertificatesFromEdgelet(workloadUri, Constants.WorkloadApiVersion, moduleId, generationId, edgeHubHostname, expiration);
                this.serverCertificates = (certificates.ServerCertificate, certificates.CertificateChain?.ToList());
            }
            else
            {
                string edgeHubCertPath = this.configuration.GetValue<string>(Constants.ConfigKey.EdgeHubServerCertificateFile);
                var cert = new X509Certificate2(edgeHubCertPath);
                string edgeHubCaChainCertPath = this.configuration.GetValue<string>(Constants.ConfigKey.EdgeHubServerCAChainCertificateFile);
                IList<X509Certificate2> chain = CertificateHelper.GetServerCACertificatesFromFile(edgeHubCaChainCertPath)?.ToList();
                this.serverCertificates = (cert, chain);
            }

            if (this.serverCertificates.CertificateChain != null)
            {
                CertificateHelper.InstallCerts(
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StoreName.CertificateAuthority : StoreName.Root,
                    StoreLocation.CurrentUser,
                    this.serverCertificates.CertificateChain);
            }

            this.isLoaded = true;
        }

        public void LogStatus(ILogger logger)
        {
            if (!this.isLoaded)
            {
                throw new InvalidOperationException("This method should not be called before LoadAsync method call is completed.");
            }

            if (this.serverCertificates.CertificateChain == null)
            {
                logger.LogWarning("Unable to find intermediate certificates.");
            }
            else
            {
                logger.LogInformation("Found intermediate certificates.");
            }
        }
    }
}
