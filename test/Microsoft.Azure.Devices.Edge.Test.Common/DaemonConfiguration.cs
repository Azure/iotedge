// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DaemonConfiguration
    {
        const string GlobalEndPoint = "https://global.azure-devices-provisioning.net";
        readonly string edgedConfigFile;
        readonly YamlDocument edgedConfig;

        public DaemonConfiguration(string edgedConfigFile, Option<string> agentImage, Option<Registry> agentRegistry)
        {
            this.edgedConfigFile = edgedConfigFile;
            string contents = File.ReadAllText(this.edgedConfigFile);
            this.edgedConfig = new YamlDocument(contents);
            this.UpdateAgentImage(
                agentImage.GetOrElse("mcr.microsoft.com/azureiotedge-agent:1.0"),
                agentRegistry);
        }

        public void UpdateAgentImage(string agentImage, Option<Registry> agentRegistry)
        {
            this.edgedConfig.ReplaceOrAdd("agent.config.image", agentImage);
            agentRegistry.ForEach(
                r =>
                {
                    this.edgedConfig.ReplaceOrAdd("agent.config.auth.serveraddress", r.Address);
                    this.edgedConfig.ReplaceOrAdd("agent.config.auth.username", r.Username);
                    this.edgedConfig.ReplaceOrAdd("agent.config.auth.password", r.Password);
                });
        }

        public void AddHttpsProxy(Uri proxy)
        {
            this.edgedConfig.ReplaceOrAdd("agent.env.https_proxy", proxy.ToString());
            // The config.yaml file is configured during test suite
            // initialization, before we know which protocol a given test
            // will use. Always use AmqpWs, and when each test deploys a
            // configuration, it can use whatever it wants.
            this.edgedConfig.ReplaceOrAdd("agent.env.UpstreamProtocol", "AmqpWs");
        }

        void SetBasicDpsParam(string idScope)
        {
            this.edgedConfig.RemoveIfExists("provisioning");
            this.edgedConfig.ReplaceOrAdd("provisioning.source", "dps");
            this.edgedConfig.ReplaceOrAdd("provisioning.global_endpoint", GlobalEndPoint);
            this.edgedConfig.ReplaceOrAdd("provisioning.scope_id", idScope);
        }

        public void SetDeviceConnectionString(string connectionString)
        {
            this.edgedConfig.RemoveIfExists("provisioning");
            this.edgedConfig.ReplaceOrAdd("provisioning.source", "manual");
            this.edgedConfig.ReplaceOrAdd("provisioning.device_connection_string", connectionString);
        }

        public void SetDeviceManualX509(string hubhostname, string deviceId, string identityCertPath, string identity_pk_path)
        {
            Uri certUri = new Uri(identityCertPath, UriKind.Absolute);
            Uri pKeyUri = new Uri(identity_pk_path, UriKind.Absolute);

            this.edgedConfig.RemoveIfExists("provisioning");
            this.edgedConfig.ReplaceOrAdd("provisioning.source", "manual");
            this.edgedConfig.ReplaceOrAdd("provisioning.authentication.method", "x509");
            this.edgedConfig.ReplaceOrAdd("provisioning.authentication.iothub_hostname", hubhostname);
            this.edgedConfig.ReplaceOrAdd("provisioning.authentication.device_id", deviceId);
            this.edgedConfig.ReplaceOrAdd("provisioning.authentication.identity_cert", certUri.ToString());
            this.edgedConfig.ReplaceOrAdd("provisioning.authentication.identity_pk", pKeyUri.ToString());
        }

        public void SetDpsSymmetricKey(string idScope, string registrationId, string deviceKey)
        {
            this.SetBasicDpsParam(idScope);
            this.edgedConfig.ReplaceOrAdd("provisioning.attestation.method", "symmetric_key");
            this.edgedConfig.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);
            this.edgedConfig.ReplaceOrAdd("provisioning.attestation.symmetric_key", deviceKey);
        }

        public void SetDpsX509(string idScope, string registrationId, IdCertificates cert)
        {
            Uri certUri = new Uri(cert.CertificatePath, UriKind.Absolute);
            Uri pKeyUri = new Uri(cert.KeyPath, UriKind.Absolute);

            this.SetBasicDpsParam(idScope);
            this.edgedConfig.ReplaceOrAdd("provisioning.attestation.method", "x509");
            this.edgedConfig.ReplaceOrAdd("provisioning.attestation.identity_cert", certUri.ToString());
            this.edgedConfig.ReplaceOrAdd("provisioning.attestation.identity_pk", pKeyUri.ToString());
            this.edgedConfig.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);
        }

        public void SetDeviceHostname(string value)
        {
            this.edgedConfig.ReplaceOrAdd("hostname", value);
        }

        public void SetParentHostname(string value)
        {
            this.edgedConfig.ReplaceOrAdd("parent_hostname", value);
        }

        public void SetCertificates(CaCertificates certs)
        {
            this.edgedConfig.ReplaceOrAdd("certificates.device_ca_cert", certs.CertificatePath);
            this.edgedConfig.ReplaceOrAdd("certificates.device_ca_pk", certs.KeyPath);
            this.edgedConfig.ReplaceOrAdd("certificates.trusted_ca_certs", certs.TrustedCertificatesPath);
        }

        public void RemoveCertificates()
        {
            this.edgedConfig.RemoveIfExists("certificates");
        }

        public void Update()
        {
            var attr = File.GetAttributes(this.edgedConfigFile);
            File.SetAttributes(this.edgedConfigFile, attr & ~FileAttributes.ReadOnly);

            File.WriteAllText(this.edgedConfigFile, this.edgedConfig.ToString());

            if (attr != 0)
            {
                File.SetAttributes(this.edgedConfigFile, attr);
            }
        }
    }
}
