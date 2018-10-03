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
        enum OperatingMode
        {
            None,
            Docker,
            IotEdged,
            IotEdgedDev
        }

        public X509Certificate2 ServerCertificate { get; }

        public IList<X509Certificate2> CertificateChain { get; }

        EdgeHubCertificates(X509Certificate2 serverCertificate, IList<X509Certificate2> certificateChain)
        {
            this.ServerCertificate = serverCertificate;
            this.CertificateChain = certificateChain;
        }

        public static async Task<EdgeHubCertificates> LoadAsync(IConfigurationRoot configuration)
        {
            Preconditions.CheckNotNull(configuration, nameof(configuration));
            string edgeHubDevCertPath = configuration.GetValue<string>(Constants.ConfigKey.EdgeHubDevServerCertificateFile);
            string edgeHubDevPrivateKeyPath = configuration.GetValue<string>(Constants.ConfigKey.EdgeHubDevServerPrivateKeyFile);
            string edgeHubDockerCertPFXPath = configuration.GetValue<string>(Constants.ConfigKey.EdgeHubServerCertificateFile);
            string edgeHubDockerCaChainCertPath = configuration.GetValue<string>(Constants.ConfigKey.EdgeHubServerCAChainCertificateFile);
            string edgeHubConnectionString = configuration.GetValue<string>(Constants.ConfigKey.IotHubConnectionString);

            OperatingMode mode;
            if (string.IsNullOrEmpty(edgeHubConnectionString))
            {
                // When connection string is not set it is edged mode as iotedgd is expected to set this
                mode = OperatingMode.IotEdged;
            }
            else if (!string.IsNullOrEmpty(edgeHubDevCertPath) &&
                     !string.IsNullOrEmpty(edgeHubDevPrivateKeyPath))
            {
                mode = OperatingMode.IotEdgedDev;
            }
            else if (!string.IsNullOrEmpty(edgeHubDockerCertPFXPath) &&
                     !string.IsNullOrEmpty(edgeHubDockerCaChainCertPath))
            {
                mode = OperatingMode.Docker;
            }
            else
            {
                mode = OperatingMode.None;
            }


            EdgeHubCertificates result;
            switch (mode)
            {
                case OperatingMode.Docker:
                    List<X509Certificate2> certificateChain = CertificateHelper.GetServerCACertificatesFromFile(edgeHubDockerCaChainCertPath)?.ToList();
                    InstallCertificates(certificateChain);
                    result = new EdgeHubCertificates(new X509Certificate2(edgeHubDockerCertPFXPath), certificateChain);
                    break;
                case OperatingMode.IotEdged:
                case OperatingMode.IotEdgedDev:
                    (X509Certificate2 ServerCertificate, IEnumerable<X509Certificate2> CertificateChain) certificates;
                    if (mode == OperatingMode.IotEdgedDev)
                    {
                        certificates = CertificateHelper.GetServerCertificateAndChainFromFile(edgeHubDevCertPath, edgeHubDevPrivateKeyPath);
                    }
                    else
                    {
                        // reach out to the iotedged via the workload interface
                        var workloadUri = new Uri(configuration.GetValue<string>(Constants.ConfigKey.WorkloadUri));
                        string edgeHubHostname = configuration.GetValue<string>(Constants.ConfigKey.EdgeDeviceHostName);
                        string moduleId = configuration.GetValue<string>(Constants.ConfigKey.ModuleId);
                        string generationId = configuration.GetValue<string>(Constants.ConfigKey.ModuleGenerationId);
                        DateTime expiration = DateTime.UtcNow.AddDays(Constants.CertificateValidityDays);
                        certificates = await CertificateHelper.GetServerCertificatesFromEdgelet(workloadUri, Constants.WorkloadApiVersion, moduleId, generationId, edgeHubHostname, expiration);
                    }
                    InstallCertificates(certificates.CertificateChain);
                    result = new EdgeHubCertificates(certificates.ServerCertificate, certificates.CertificateChain?.ToList());
                    break;
                default:
                    throw new InvalidOperationException("Edge Hub certificate files incorrectly configured");
            };

            return result;
        }

        static void InstallCertificates(IEnumerable<X509Certificate2> certificateChain)
        {
            string message;
            if (certificateChain != null)
            {
                message = "Found intermediate certificates.";

                CertificateHelper.InstallCerts(
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StoreName.CertificateAuthority : StoreName.Root,
                    StoreLocation.CurrentUser,
                    certificateChain);
            }
            else
            {
                message = "Unable to find intermediate certificates.";
            }

            Console.WriteLine($"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)}] {message}");
        }
    }
}
