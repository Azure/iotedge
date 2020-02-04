// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;

    public class DaemonConfiguration
    {
        const string GlobalEndPoint = "https://global.azure-devices-provisioning.net";
        readonly string configYamlFile;
        readonly YamlDocument config;

        public DaemonConfiguration(string configYamlFile)
        {
            this.configYamlFile = configYamlFile;
            string contents = File.ReadAllText(this.configYamlFile);
            this.config = new YamlDocument(contents);
        }

        public void AddHttpsProxy(Uri proxy)
        {
            this.config.ReplaceOrAdd("agent.env.https_proxy", proxy.ToString());
            // The config.yaml file is configured during test suite
            // initialization, before we know which protocol a given test
            // will use. Always use AmqpWs, and when each test deploys a
            // configuration, it can use whatever it wants.
            this.config.ReplaceOrAdd("agent.env.UpstreamProtocol", "AmqpWs");
        }

        void SetBasicDpsParam(string idScope)
        {
            this.config.RemoveIfExists("provisioning");
            this.config.ReplaceOrAdd("provisioning.source", "dps");
            this.config.ReplaceOrAdd("provisioning.global_endpoint", GlobalEndPoint);
            this.config.ReplaceOrAdd("provisioning.scope_id", idScope);
        }

        public void SetDeviceConnectionString(string connectionString)
        {
            this.config.RemoveIfExists("provisioning");
            this.config.ReplaceOrAdd("provisioning.source", "manual");
            this.config.ReplaceOrAdd("provisioning.device_connection_string", connectionString);
        }

        public void SetDeviceManualX509(string hubhostname, string deviceId, string identityCertPath, string identity_pk_path)
        {
            Uri certUri = new Uri(identityCertPath, UriKind.Absolute);
            Uri pKeyUri = new Uri(identity_pk_path, UriKind.Absolute);

            this.config.RemoveIfExists("provisioning");
            this.config.ReplaceOrAdd("provisioning.source", "manual");
            this.config.ReplaceOrAdd("provisioning.authentication.method", "x509");
            this.config.ReplaceOrAdd("provisioning.authentication.iothub_hostname", hubhostname);
            this.config.ReplaceOrAdd("provisioning.authentication.device_id", deviceId);
            this.config.ReplaceOrAdd("provisioning.authentication.identity_cert", certUri.ToString());
            this.config.ReplaceOrAdd("provisioning.authentication.identity_pk", pKeyUri.ToString());
        }

        public void SetDpsSymmetricKey(string idScope, string registrationId, string deviceKey)
        {
            this.SetBasicDpsParam(idScope);
            this.config.ReplaceOrAdd("provisioning.attestation.method", "symmetric_key");
            this.config.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);
            this.config.ReplaceOrAdd("provisioning.attestation.symmetric_key", deviceKey);
        }

        public void SetDpsX509(string idScope, string registrationId, IdCertificates cert)
        {
            Uri certUri = new Uri(cert.CertificatePath, UriKind.Absolute);
            Uri pKeyUri = new Uri(cert.KeyPath, UriKind.Absolute);

            this.SetBasicDpsParam(idScope);
            this.config.ReplaceOrAdd("provisioning.attestation.method", "x509");
            this.config.ReplaceOrAdd("provisioning.attestation.identity_cert", certUri.ToString());
            this.config.ReplaceOrAdd("provisioning.attestation.identity_pk", pKeyUri.ToString());
            this.config.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);
        }

        public void SetDeviceHostname(string value)
        {
            this.config.ReplaceOrAdd("hostname", value);
        }

        public void SetCertificates(CaCertificates certs)
        {
            this.config.ReplaceOrAdd("certificates.device_ca_cert", certs.CertificatePath);
            this.config.ReplaceOrAdd("certificates.device_ca_pk", certs.KeyPath);
            this.config.ReplaceOrAdd("certificates.trusted_ca_certs", certs.TrustedCertificatesPath);
        }

        public void RemoveCertificates()
        {
            this.config.RemoveIfExists("certificates");
        }

        public void Update()
        {
            var attr = File.GetAttributes(this.configYamlFile);
            File.SetAttributes(this.configYamlFile, attr & ~FileAttributes.ReadOnly);

            File.WriteAllText(this.configYamlFile, this.config.ToString());

            if (attr != 0)
            {
                File.SetAttributes(this.configYamlFile, attr);
            }
        }
    }
}
