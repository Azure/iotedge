// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;

    public struct ConfigFilePaths
    {
        public string Keyd;
        public string Certd;
        public string Identityd;
        public string Edged;
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
            public string Path;
            public IConfigDocument Document;
        }

        const string GlobalEndPoint = "https://global.azure-devices-provisioning.net";
        private Dictionary<Service, Config> config;
        private string principalsPath;

        public DaemonConfiguration(ConfigFilePaths configFiles, uint iotedgeUid)
        {
            this.config = new Dictionary<Service, Config>();

            this.InitServiceConfig(Service.Keyd, configFiles.Keyd, true);
            this.InitServiceConfig(Service.Certd, configFiles.Certd, true);
            this.InitServiceConfig(Service.Identityd, configFiles.Identityd, true);
            this.InitServiceConfig(Service.Edged, configFiles.Edged, false);

            string principalsPath = Path.Combine(
                Path.GetDirectoryName(configFiles.Identityd),
                "config.d");
            this.principalsPath = principalsPath;

            // Clear any existing Identity Service principals.
            this.config[Service.Identityd].Document.RemoveIfExists("principal");
            if (Directory.Exists(principalsPath))
            {
                Directory.Delete(principalsPath, true);
            }

            Directory.CreateDirectory(principalsPath);
            OsPlatform.Current.SetOwner(principalsPath, "aziotid", "755");

            // Add the principal entry for aziot-edge to Identity Service.
            // This is required so aziot-edge can communicate with Identity Service.
            this.AddPrincipal("aziot-edge", iotedgeUid);
        }

        public void AddHttpsProxy(Uri proxy)
        {
            this.config[Service.Edged].Document.ReplaceOrAdd("agent.env.https_proxy", proxy.ToString());
            // The config.yaml file is configured during test suite
            // initialization, before we know which protocol a given test
            // will use. Always use AmqpWs, and when each test deploys a
            // configuration, it can use whatever it wants.
            this.config[Service.Edged].Document.ReplaceOrAdd("agent.env.UpstreamProtocol", "AmqpWs");
        }

        void InitServiceConfig(Service service, string path, bool toml)
        {
            Config config;
            string contents = File.ReadAllText(path);

            if (toml)
            {
                config.Document = new TomlDocument(contents);
            }
            else
            {
                config.Document = new YamlDocument(contents);
            }

            config.Path = path;
            this.config.Add(service, config);
        }

        void SetBasicDpsParam(string idScope)
        {
            this.config[Service.Identityd].Document.RemoveIfExists("provisioning");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.always_reprovision_on_startup", true);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.source", "dps");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.global_endpoint", GlobalEndPoint);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.scope_id", idScope);
        }

        public void SetManualSasProvisioning(string hubHostname, string deviceId, string key)
        {
            string keyName = DaemonConfiguration.SanitizeName(deviceId);
            this.CreatePreloadedKey(keyName, key);

            this.config[Service.Identityd].Document.RemoveIfExists("provisioning");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.always_reprovision_on_startup", true);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.source", "manual");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.iothub_hostname", hubHostname);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.device_id", deviceId);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.authentication.method", "sas");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.authentication.device_id_pk", keyName);
        }

        public void SetDeviceManualX509(string hubhostname, string deviceId, string identityCertPath, string identityPkPath)
        {
            if (!File.Exists(identityCertPath))
            {
                throw new InvalidOperationException($"{identityCertPath} does not exist");
            }

            if (!File.Exists(identityPkPath))
            {
                throw new InvalidOperationException($"{identityPkPath} does not exist");
            }

            this.config[Service.Identityd].Document.RemoveIfExists("provisioning");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.always_reprovision_on_startup", true);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.source", "manual");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.iothub_hostname", hubhostname);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.device_id", deviceId);

            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.authentication.method", "x509");

            string certFileName = Path.GetFileName(identityCertPath);
            string certName = DaemonConfiguration.SanitizeName(certFileName);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.authentication.identity_cert", certName);
            this.config[Service.Certd].Document.ReplaceOrAdd($"preloaded_certs.{certName}", "file://" + identityCertPath);

            string keyFileName = Path.GetFileName(identityPkPath);
            string keyName = DaemonConfiguration.SanitizeName(keyFileName);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.authentication.identity_pk", keyName);
            this.config[Service.Keyd].Document.ReplaceOrAdd($"preloaded_keys.{keyName}", "file://" + identityPkPath);
        }

        public void SetDpsSymmetricKey(string idScope, string registrationId, string deviceKey)
        {
            this.SetBasicDpsParam(idScope);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.method", "symmetric_key");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);

            string keyName = DaemonConfiguration.SanitizeName($"dps-symmetric-key-{registrationId}");
            this.CreatePreloadedKey(keyName, deviceKey);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.symmetric_key", keyName);
        }

        public void SetDpsX509(string idScope, string registrationId, IdCertificates cert, string trustBundle)
        {
            if (!File.Exists(cert.CertificatePath))
            {
                throw new InvalidOperationException($"{cert.CertificatePath} does not exist");
            }

            if (!File.Exists(cert.KeyPath))
            {
                throw new InvalidOperationException($"{cert.KeyPath} does not exist");
            }

            if (!File.Exists(trustBundle))
            {
                throw new InvalidOperationException($"{trustBundle} does not exist");
            }

            this.SetBasicDpsParam(idScope);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.method", "x509");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);

            string certFileName = Path.GetFileName(cert.CertificatePath);
            string certName = DaemonConfiguration.SanitizeName(certFileName);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.identity_cert", certName);
            this.config[Service.Certd].Document.ReplaceOrAdd($"preloaded_certs.{certName}", "file://" + cert.CertificatePath);

            string keyFileName = Path.GetFileName(cert.KeyPath);
            string keyName = DaemonConfiguration.SanitizeName(keyFileName);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.identity_pk", keyName);
            this.config[Service.Keyd].Document.ReplaceOrAdd($"preloaded_keys.{keyName}", "file://" + cert.KeyPath);

            this.config[Service.Certd].Document.ReplaceOrAdd("preloaded_certs.aziot-edged-trust-bundle", "file://" + trustBundle);
        }

        public void SetEdgeAgentImage(string value)
        {
            this.config[Service.Edged].Document.ReplaceOrAdd("agent.config.image", value);
        }

        public void SetDeviceHostname(string value)
        {
            this.config[Service.Edged].Document.ReplaceOrAdd("hostname", value);
            this.config[Service.Identityd].Document.ReplaceOrAdd("hostname", value);
        }

        public void SetParentHostname(string value)
        {
            this.config[Service.Edged].Document.ReplaceOrAdd("parent_hostname", value);
        }

        public void SetCertificates(CaCertificates certs)
        {
            if (!File.Exists(certs.CertificatePath))
            {
                throw new InvalidOperationException($"{certs.CertificatePath} does not exist");
            }

            if (!File.Exists(certs.KeyPath))
            {
                throw new InvalidOperationException($"{certs.KeyPath} does not exist");
            }

            if (!File.Exists(certs.TrustedCertificatesPath))
            {
                throw new InvalidOperationException($"{certs.TrustedCertificatesPath} does not exist");
            }

            this.config[Service.Certd].Document.ReplaceOrAdd("preloaded_certs.aziot-edged-ca", "file://" + certs.CertificatePath);
            this.config[Service.Keyd].Document.ReplaceOrAdd("preloaded_keys.aziot-edged-ca", "file://" + certs.KeyPath);
            this.config[Service.Certd].Document.ReplaceOrAdd("preloaded_certs.aziot-edged-trust-bundle", "file://" + certs.TrustedCertificatesPath);
        }

        public void RemoveCertificates()
        {
            this.config[Service.Certd].Document.RemoveIfExists("preloaded_certs.aziot-edged-ca");
            this.config[Service.Keyd].Document.RemoveIfExists("preloaded_keys.aziot-edged-ca");
            this.config[Service.Certd].Document.RemoveIfExists("preloaded_certs.aziot-edged-trust-bundle");
        }

        public void AddPrincipal(string name, uint uid, string[] type = null, Dictionary<string, string> opts = null)
        {
            string path = Path.Combine(this.principalsPath, $"{name}-principal.toml");

            string principal = string.Join(
                "\n",
                "[[principal]]",
                $"uid = {uid}",
                $"name = \"{name}\"");

            if (type != null)
            {
                // Need to quote each type.
                for (int i = 0; i < type.Length; i++)
                {
                    type[i] = $"\"{type[i]}\"";
                }

                string types = string.Join(", ", type);
                principal = string.Join("\n", principal, $"idtype = [{types}]");
            }

            if (opts != null)
            {
                foreach (KeyValuePair<string, string> opt in opts)
                {
                    principal = string.Join("\n", principal, $"{opt.Key} = {opt.Value}");
                }
            }

            File.WriteAllText(path, principal + "\n");
            OsPlatform.Current.SetOwner(path, "aziotid", "644");
        }

        public void Update()
        {
            foreach (KeyValuePair<Service, Config> i in this.config)
            {
                string path = i.Value.Path;
                var attr = File.GetAttributes(path);
                File.SetAttributes(path, attr & ~FileAttributes.ReadOnly);

                File.WriteAllText(path, i.Value.Document.ToString());

                if (attr != 0)
                {
                    File.SetAttributes(path, attr);
                }
            }
        }

        public static void CreateConfigFile(string configFile, string defaultFile, string owner)
        {
            // If the config file does not exist, create it from the default file.
            // If the default file does not exist, create an empty config file.
            if (!File.Exists(configFile))
            {
                if (File.Exists(defaultFile))
                {
                    File.Copy(defaultFile, configFile);
                }
                else
                {
                    File.Create(configFile).Dispose();
                }
            }

            // Change owner of config file.
            OsPlatform.Current.SetOwner(configFile, owner, "644");
        }

        private static string SanitizeName(string name)
        {
            // Due to '.' being used as a delimiter for config file tables, names cannot contain '.'
            // Strip non-alphanumeric characters except for '-' for a safe name.
            return Regex.Replace(name, "[^A-Za-z0-9 -]", string.Empty);
        }

        // All names passed to this function must be sanitized with DaemonConfiguration.SanitizeName
        private void CreatePreloadedKey(string name, string value)
        {
            string filePath = $"/etc/aziot/e2e_tests/{name}.key";

            File.WriteAllBytes(filePath, Convert.FromBase64String(value));
            OsPlatform.Current.SetOwner(filePath, "aziotks", "600");

            this.config[Service.Keyd].Document.ReplaceOrAdd($"preloaded_keys.{name}", "file://" + filePath);
        }
    }
}
