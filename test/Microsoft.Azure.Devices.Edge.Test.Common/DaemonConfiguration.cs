// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
        public enum Service
        {
            Keyd,
            Certd,
            Identityd,
            Edged
        }

        private struct Config
        {
            public string ConfigPath;
            public string PrincipalsPath;
            public string Owner;
            public uint Uid;
            public TomlDocument Document;
        }

        const string GlobalEndPoint = "https://global.azure-devices-provisioning.net";
        private Dictionary<Service, Config> config;

        public DaemonConfiguration(ConfigFilePaths configFiles)
        {
            this.config = new Dictionary<Service, Config>();

            this.InitServiceConfig(Service.Keyd, configFiles.Keyd, "aziotks");
            this.InitServiceConfig(Service.Certd, configFiles.Certd, "aziotcs");
            this.InitServiceConfig(Service.Identityd, configFiles.Identityd, "aziotid");
            this.InitServiceConfig(Service.Edged, configFiles.Edged, "iotedge");
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

        void InitServiceConfig(Service service, string path, string owner)
        {
            Config config;
            string contents = File.ReadAllText(path);

            config.Document = new TomlDocument(contents);

            config.ConfigPath = path;
            config.PrincipalsPath = Path.Combine(
                Path.GetDirectoryName(path),
                "config.d");
            config.Owner = owner;
            config.Uid = OsPlatform.Current.GetUid(owner);

            this.config.Add(service, config);
        }

        void SetBasicDpsParam(string idScope)
        {
            this.config[Service.Edged].Document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");

            this.config[Service.Identityd].Document.RemoveIfExists("provisioning");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.source", "dps");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.global_endpoint", GlobalEndPoint);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.scope_id", idScope);
        }

        void SetAuth(string keyName)
        {
            this.AddAuthPrincipal(
                Service.Keyd,
                "aziot-identityd",
                this.config[Service.Identityd].Uid,
                new string[] { keyName, "aziot_identityd_master_id" });
            this.AddIdentityPrincipal("aziot-edged", this.config[Service.Edged].Uid);
            this.AddAuthPrincipal(
                Service.Keyd,
                "aziot-edged",
                this.config[Service.Edged].Uid,
                new string[] { "iotedge_master_encryption_id", "aziot-edged-ca" });
            this.AddAuthPrincipal(
                Service.Certd,
                "aziot-edged",
                this.config[Service.Edged].Uid,
                new string[] { "aziot-edged/module/*" });
        }

        public void SetManualSasProvisioning(string hubHostname, Option<string> parentHostname, string deviceId, string key)
        {
            string keyName = DaemonConfiguration.SanitizeName(deviceId);
            this.CreatePreloadedKey(keyName, key);

            this.config[Service.Identityd].Document.RemoveIfExists("provisioning");
            parentHostname.ForEach(
                parent_hostame =>
                this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.local_gateway_hostname", parent_hostame));
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.source", "manual");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.iothub_hostname", hubHostname);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.device_id", deviceId);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.authentication.method", "sas");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.authentication.device_id_pk", keyName);

            this.config[Service.Edged].Document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");

            this.SetAuth(keyName);
        }

        public void SetDeviceManualX509(string hubhostname, Option<string> parentHostname, string deviceId, string identityCertPath, string identityPkPath)
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
            parentHostname.ForEach(
                parent_hostame =>
                this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.local_gateway_hostname", parent_hostame));
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

            this.config[Service.Edged].Document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");

            this.SetAuth(keyName);
        }

        public void SetDpsSymmetricKey(string idScope, string registrationId, string deviceKey)
        {
            this.SetBasicDpsParam(idScope);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.method", "symmetric_key");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);

            string keyName = DaemonConfiguration.SanitizeName($"dps-symmetric-key-{registrationId}");
            this.CreatePreloadedKey(keyName, deviceKey);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.symmetric_key", keyName);

            this.SetAuth(keyName);
        }

        public void SetDpsX509(string idScope, string registrationId, string identityCertPath, string identityPkPath, string trustBundle)
        {
            if (!File.Exists(identityCertPath))
            {
                throw new InvalidOperationException($"{identityCertPath} does not exist");
            }

            if (!File.Exists(identityPkPath))
            {
                throw new InvalidOperationException($"{identityPkPath} does not exist");
            }

            if (!File.Exists(trustBundle))
            {
                throw new InvalidOperationException($"{trustBundle} does not exist");
            }

            this.SetBasicDpsParam(idScope);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.method", "x509");
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);

            string certFileName = Path.GetFileName(identityCertPath);
            string certName = DaemonConfiguration.SanitizeName(certFileName);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.identity_cert", certName);
            this.config[Service.Certd].Document.ReplaceOrAdd($"preloaded_certs.{certName}", "file://" + identityCertPath);

            string keyFileName = Path.GetFileName(identityPkPath);
            string keyName = DaemonConfiguration.SanitizeName(keyFileName);
            this.config[Service.Identityd].Document.ReplaceOrAdd("provisioning.attestation.identity_pk", keyName);
            this.config[Service.Keyd].Document.ReplaceOrAdd($"preloaded_keys.{keyName}", "file://" + identityPkPath);

            this.config[Service.Certd].Document.ReplaceOrAdd("preloaded_certs.aziot-edged-trust-bundle", "file://" + trustBundle);

            this.SetAuth(keyName);
        }

        public void SetEdgeAgentImage(string value, IEnumerable<Registry> registries)
        {
            this.config[Service.Edged].Document.ReplaceOrAdd("agent.config.image", value);

            // Currently, the only place for registries is [agent.config.auth]
            // So only one registry is supported.
            if (registries.Count() > 1)
            {
                throw new ArgumentException("Currently, up to a single registry is supported");
            }

            foreach (Registry registry in registries)
            {
                this.config[Service.Edged].Document.ReplaceOrAdd("agent.config.auth.serveraddress", registry.Address);
                this.config[Service.Edged].Document.ReplaceOrAdd("agent.config.auth.username", registry.Username);
                this.config[Service.Edged].Document.ReplaceOrAdd("agent.config.auth.password", registry.Password);
            }
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

            if (certs.ManifestTrustedCertificatesPath.HasValue && File.Exists(certs.ManifestTrustedCertificatesPath.OrDefault()))
            {
                this.config[Service.Certd].Document.ReplaceOrAdd("preloaded_certs.aziot-edged-manifest-trust-bundle", "file://" + certs.ManifestTrustedCertificatesPath.OrDefault());
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

        public void AddIdentityPrincipal(string name, uint uid, string[] type = null, Dictionary<string, string> opts = null)
        {
            string path = Path.Combine(this.config[Service.Identityd].PrincipalsPath, $"{name}-principal.toml");

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
            OsPlatform.Current.SetOwner(path, this.config[Service.Identityd].Owner, "644");
        }

        public void AddAuthPrincipal(Service service, string name, uint uid, string[] credentials)
        {
            if (credentials == null || credentials.Length == 0)
            {
                throw new ArgumentException("Empty array of credentials");
            }

            string auth = string.Empty;

            switch (service)
            {
                case Service.Keyd:
                    auth += "keys = [";
                    break;
                case Service.Certd:
                    auth += "certs = [";
                    break;
                default:
                    throw new ArgumentException("Authorization is only relevant for keyd and certd");
            }

            for (int i = 0; i < credentials.Length; i++)
            {
                credentials[i] = $"\"{credentials[i]}\"";
            }

            auth += string.Join(", ", credentials);
            auth += "]";

            string path = Path.Combine(this.config[service].PrincipalsPath, $"{name}-principal.toml");

            string principal = string.Join(
                "\n",
                "[[principal]]",
                $"uid = {uid}",
                auth);

            File.WriteAllText(path, principal + "\n");
            OsPlatform.Current.SetOwner(path, this.config[service].Owner, "644");
        }

        public void Update()
        {
            foreach (KeyValuePair<Service, Config> i in this.config)
            {
                string path = i.Value.ConfigPath;
                var attr = File.GetAttributes(path);
                File.SetAttributes(path, attr & ~FileAttributes.ReadOnly);

                File.WriteAllText(path, i.Value.Document.ToString());

                if (attr != 0)
                {
                    File.SetAttributes(path, attr);
                }
            }
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
            string filePath = Path.Combine(FixedPaths.E2E_TEST_DIR, $"{name}.key");

            File.WriteAllBytes(filePath, Convert.FromBase64String(value));
            OsPlatform.Current.SetOwner(filePath, this.config[Service.Keyd].Owner, "600");

            this.config[Service.Keyd].Document.ReplaceOrAdd($"preloaded_keys.{name}", "file://" + filePath);
        }
    }
}
