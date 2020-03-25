// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;

    public class DaemonConfiguration : IDaemonConfiguration
    {
        const string GlobalEndPoint = "https://global.azure-devices-provisioning.net";
        readonly string configYamlFile;
        readonly YamlDocument config;
        readonly List<(string, string)> k8sCommands;

        public DaemonConfiguration(string configYamlFile)
        {
            this.configYamlFile = configYamlFile;
            if (File.Exists(this.configYamlFile))
            {
                string contents = File.ReadAllText(this.configYamlFile);
                this.config = new YamlDocument(contents);
            }
            else
            {
                this.config = new YamlDocument(string.Empty);
            }

            this.config.ReplaceOrAdd("iotedged.env.IOTEDGE_LOG", "debug");
            this.k8sCommands = new List<(string, string)>();
        }

        public List<(string, string)> GetK8sCommands() => this.k8sCommands;

        public void AddHttpsProxy(Uri proxy)
        {
            this.config.ReplaceOrAdd("iotedged.data.httpsProxy", proxy.ToString());
            // The config.yaml file is configured during test suite
            // initialization, before we know which protocol a given test
            // will use. Always use AmqpWs, and when each test deploys a
            // configuration, it can use whatever it wants.
            this.config.ReplaceOrAdd("edgeAgent.env.upstreamProtocol", "AmqpWs");
        }

        void SetBasicDpsParam(string idScope)
        {
            this.config.RemoveIfExists("provisioning");
            this.config.ReplaceOrAdd("provisioning.source", "dps");
            this.config.ReplaceOrAdd("provisioning.globalEndpoint", GlobalEndPoint);
            this.config.ReplaceOrAdd("provisioning.scopeId", idScope);
        }

        public void SetDeviceConnectionString(string connectionString)
        {
            this.config.RemoveIfExists("provisioning");
            this.config.ReplaceOrAdd("provisioning.source", "manual");
            this.config.ReplaceOrAdd("provisioning.deviceConnectionString", connectionString);
        }

        public void SetDeviceManualX509(string hubhostname, string deviceId, string identityCertPath, string identity_pk_path)
        {
            // 1. construct a command to make a secret out of the data given.
            this.k8sCommands.Add(("kubectl", $"create secret generic -n {Constants.Deployment} {Constants.AuthenticationSecret} --from-file={identityCertPath} --from-file={identity_pk_path}"));

            // 2.load configuration
            string identityCertSecretName = Path.GetFileName(identityCertPath);
            string identityPkSecretName = Path.GetFileName(identity_pk_path);

            this.config.RemoveIfExists("provisioning");
            this.config.ReplaceOrAdd("provisioning.source", "manual");
            this.config.ReplaceOrAdd("provisioning.authentication.method", "x509");
            this.config.ReplaceOrAdd("provisioning.authentication.secret", Constants.AuthenticationSecret);
            this.config.ReplaceOrAdd("provisioning.authentication.iothubHostname", hubhostname);
            this.config.ReplaceOrAdd("provisioning.authentication.deviceId", deviceId);
            this.config.ReplaceOrAdd("provisioning.authentication.identityCert", identityCertSecretName);
            this.config.ReplaceOrAdd("provisioning.authentication.identityPk", identityPkSecretName);
        }

        public void SetDpsSymmetricKey(string idScope, string registrationId, string deviceKey)
        {
            this.SetBasicDpsParam(idScope);
            this.config.ReplaceOrAdd("provisioning.attestation.method", "symmetric_key");
            this.config.ReplaceOrAdd("provisioning.attestation.registrationId", registrationId);
            this.config.ReplaceOrAdd("provisioning.attestation.symmetricKey", deviceKey);
        }

        public void SetDpsX509(string idScope, string registrationId, IdCertificates cert)
        {
            // 1. construct a command to make a secret out of the data given.
            this.k8sCommands.Add(("kubectl", $"create secret generic -n {Constants.Deployment} {Constants.AttestationSecret} --from-file={cert.CertificatePath} --from-file={cert.KeyPath}"));

            // 2.load configuration
            string identityCertSecretName = Path.GetFileName(cert.CertificatePath);
            string identityPkSecretName = Path.GetFileName(cert.KeyPath);

            this.SetBasicDpsParam(idScope);
            this.config.ReplaceOrAdd("provisioning.attestation.method", "x509");
            this.config.ReplaceOrAdd("provisioning.attestation.secret", Constants.AttestationSecret);
            this.config.ReplaceOrAdd("provisioning.attestation.identityCert", identityCertSecretName);
            this.config.ReplaceOrAdd("provisioning.attestation.identityPk", identityPkSecretName);
            this.config.ReplaceOrAdd("provisioning.attestation.registrationId", registrationId);
        }

        public void SetDeviceHostname(string value)
        {
            // This isn't set in k8s
            this.config.ReplaceOrAdd("hostname", value);
        }

        public void SetCertificates(CaCertificates certs)
        {
            // 1. construct a command to make a secret out of the data given.
            this.k8sCommands.Add(("kubectl", $"create secret generic -n {Constants.Deployment} {Constants.CertificateSecret} --from-file={certs.CertificatePath} --from-file={certs.KeyPath} --from-file={certs.TrustedCertificatesPath}"));

            // 2.load configuration
            string caCertSecretName = Path.GetFileName(certs.CertificatePath);
            string caKeySecretName = Path.GetFileName(certs.KeyPath);
            string caTrustedCertSecretName = Path.GetFileName(certs.TrustedCertificatesPath);

            this.config.ReplaceOrAdd("certificates.device_ca_cert", caCertSecretName);
            this.config.ReplaceOrAdd("certificates.device_ca_pk", caKeySecretName);
            this.config.ReplaceOrAdd("certificates.trusted_ca_certs", caTrustedCertSecretName);
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
