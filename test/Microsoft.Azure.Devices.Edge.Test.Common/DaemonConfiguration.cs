// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;

    public struct ConfigFilePaths {
        public string keyd;
        public string certd;
        public string identityd;
        public string edged;
    }

    public class DaemonConfiguration
    {
        private enum Service
        {
            Keyd,
            Certd,
            Identityd,
            Edged
        }

        private struct Config
        {
            public string path;
            public IConfigDocument document;
        }

        const string GlobalEndPoint = "https://global.azure-devices-provisioning.net";
        private Dictionary<Service, Config> config;

        public DaemonConfiguration(ConfigFilePaths configFiles, Option<string> agentImage, Option<Registry> agentRegistry)
        {
            this.config = new Dictionary<Service, Config>();
            this.InitServiceConfig(Service.Keyd, configFiles.keyd, true);
            this.InitServiceConfig(Service.Certd, configFiles.certd, true);
            this.InitServiceConfig(Service.Identityd, configFiles.identityd, true);
            this.InitServiceConfig(Service.Edged, configFiles.edged, false);

            this.UpdateAgentImage(
                agentImage.GetOrElse("mcr.microsoft.com/azureiotedge-agent:1.0"),
                agentRegistry);
        }

        public void UpdateAgentImage(string agentImage, Option<Registry> agentRegistry)
        {
            this.config[Service.Edged].document.ReplaceOrAdd("agent.config.image", agentImage);
            agentRegistry.ForEach(
                r =>
                {
                    this.config[Service.Edged].document.ReplaceOrAdd("agent.config.auth.serveraddress", r.Address);
                    this.config[Service.Edged].document.ReplaceOrAdd("agent.config.auth.username", r.Username);
                    this.config[Service.Edged].document.ReplaceOrAdd("agent.config.auth.password", r.Password);
                });
        }

        public void AddHttpsProxy(Uri proxy)
        {
            this.config[Service.Edged].document.ReplaceOrAdd("agent.env.https_proxy", proxy.ToString());
            // The config.yaml file is configured during test suite
            // initialization, before we know which protocol a given test
            // will use. Always use AmqpWs, and when each test deploys a
            // configuration, it can use whatever it wants.
            this.config[Service.Edged].document.ReplaceOrAdd("agent.env.UpstreamProtocol", "AmqpWs");
        }

        void InitServiceConfig(Service service, string path, bool toml)
        {
            Config config;
            string contents = File.ReadAllText(path);

            if(toml)
            {
                config.document = new TomlDocument(contents);
            }
            else
            {
                config.document = new YamlDocument(contents);
            }

            config.path = path;
            this.config.Add(service, config);
        }

        void SetBasicDpsParam(string idScope)
        {
            this.config[Service.Edged].document.RemoveIfExists("provisioning");
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.source", "dps");
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.global_endpoint", GlobalEndPoint);
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.scope_id", idScope);
        }

        public void SetManualSasProvisioning(string hubHostname, string deviceId, string preloadedKey)
        {
            this.config[Service.Identityd].document.RemoveIfExists("provisioning");
            this.config[Service.Identityd].document.ReplaceOrAdd("provisioning.source", "manual");
            this.config[Service.Identityd].document.ReplaceOrAdd("provisioning.iothub_hostname", hubHostname);
            this.config[Service.Identityd].document.ReplaceOrAdd("provisioning.device_id", deviceId);
            this.config[Service.Identityd].document.ReplaceOrAdd("provisioning.authentication.method", "sas");
            this.config[Service.Identityd].document.ReplaceOrAdd("provisioning.authentication.device_id_pk", preloadedKey);
        }

        public void SetDeviceManualX509(string hubhostname, string deviceId, string identityCertPath, string identity_pk_path)
        {
            Uri certUri = new Uri(identityCertPath, UriKind.Absolute);
            Uri pKeyUri = new Uri(identity_pk_path, UriKind.Absolute);

            this.config[Service.Edged].document.RemoveIfExists("provisioning");
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.source", "manual");
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.authentication.method", "x509");
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.authentication.iothub_hostname", hubhostname);
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.authentication.device_id", deviceId);
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.authentication.identity_cert", certUri.ToString());
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.authentication.identity_pk", pKeyUri.ToString());
        }

        public void SetDpsSymmetricKey(string idScope, string registrationId, string deviceKey)
        {
            this.SetBasicDpsParam(idScope);
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.attestation.method", "symmetric_key");
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.attestation.symmetric_key", deviceKey);
        }

        public void SetDpsX509(string idScope, string registrationId, IdCertificates cert)
        {
            Uri certUri = new Uri(cert.CertificatePath, UriKind.Absolute);
            Uri pKeyUri = new Uri(cert.KeyPath, UriKind.Absolute);

            this.SetBasicDpsParam(idScope);
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.attestation.method", "x509");
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.attestation.identity_cert", certUri.ToString());
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.attestation.identity_pk", pKeyUri.ToString());
            this.config[Service.Edged].document.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);
        }

        public void SetDeviceHostname(string value)
        {
            this.config[Service.Edged].document.ReplaceOrAdd("hostname", value);
            this.config[Service.Identityd].document.ReplaceOrAdd("hostname", value);
        }

        public void SetParentHostname(string value)
        {
            this.config[Service.Edged].document.ReplaceOrAdd("parent_hostname", value);
        }

        public void SetCertificates(CaCertificates certs)
        {
            this.config[Service.Edged].document.ReplaceOrAdd("certificates.device_ca_cert", certs.CertificatePath);
            this.config[Service.Edged].document.ReplaceOrAdd("certificates.device_ca_pk", certs.KeyPath);
            this.config[Service.Edged].document.ReplaceOrAdd("certificates.trusted_ca_certs", certs.TrustedCertificatesPath);
        }

        public void RemoveCertificates()
        {
            this.config[Service.Edged].document.RemoveIfExists("certificates");
        }

        public void Update()
        {
            foreach(KeyValuePair<Service, Config> i in this.config)
            {
                string path = i.Value.path;
                var attr = File.GetAttributes(path);
                File.SetAttributes(path, attr & ~FileAttributes.ReadOnly);

                File.WriteAllText(path, i.Value.document.ToString());

                if (attr != 0)
                {
                    File.SetAttributes(path, attr);
                }
            }
        }

        public void CreatePreloadedKey(string name, string value)
        {
            // The document ReplaceOrAdd and RemoveIfExists functions use '.' in their dottedKey parameter
            // to separate levels in the config document. Therefore, key names cannot contain '.'
            if(name.Contains('.'))
            {
                throw new ArgumentException("Key name cannot contain .");
            }

            string filePath = $"/etc/aziot/e2e_tests/{name}.key";

            File.WriteAllText(filePath, value);
            OsPlatform.Current.SetFileOwner(filePath, "aziotks");

            this.config[Service.Keyd].document.ReplaceOrAdd($"preloaded_keys.{name}", "file://" + filePath);
        }

        public static void CreateConfigFile(string configFile, string defaultFile, string owner)
        {
            // If the config file does not exist, create it from the default file.
            // If the default file does not exist, create an empty config file.
            if(!File.Exists(configFile))
            {
                if(File.Exists(defaultFile))
                {
                    File.Copy(defaultFile, configFile);
                }
                else
                {
                    File.Create(configFile).Dispose();
                }
            }

            // Change owner of config file.
            OsPlatform.Current.SetFileOwner(configFile, owner);
        }
    }
}
