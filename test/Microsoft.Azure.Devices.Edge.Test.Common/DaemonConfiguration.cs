// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DaemonConfiguration
    {
        public enum Service
        {
            Keyd,
            Certd,
            Identityd,
            Edged
        }

        struct Config
        {
            public string ConfigPath;
            // public string PrincipalsPath;
            // public string Owner;
            // public uint Uid;
            public TomlDocument Document;
        }

        const string GlobalEndPoint = "https://global.azure-devices-provisioning.net";
        Config config;

        public DaemonConfiguration(string superTomlPath)
        {
            string contents = File.Exists(superTomlPath) ? File.ReadAllText(superTomlPath) : string.Empty;
            this.config = new Config {
                ConfigPath = superTomlPath,
                Document = new TomlDocument(contents)
            };
        }

        public void AddHttpsProxy(Uri proxy)
        {
            this.config.Document.ReplaceOrAdd("agent.env.https_proxy", proxy.ToString());
            // The config file is configured during test suite initialization, before we know which
            // protocol a given test will use. Always use AmqpWs, and when each test deploys a
            // configuration, it can use whatever it wants.
            this.config.Document.ReplaceOrAdd("agent.env.UpstreamProtocol", "AmqpWs");
        }

        void SetBasicDpsParam(string idScope)
        {
            this.config.Document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");

            this.config.Document.RemoveIfExists("provisioning");
            this.config.Document.ReplaceOrAdd("provisioning.source", "dps");
            this.config.Document.ReplaceOrAdd("provisioning.global_endpoint", GlobalEndPoint);
            this.config.Document.ReplaceOrAdd("provisioning.id_scope", idScope);
        }

        // void SetAuth(string keyName)
        // {
        //     this.AddAuthPrincipal(
        //         Service.Keyd,
        //         "aziot-identityd",
        //         this.config[Service.Identityd].Uid,
        //         new string[] { keyName, "aziot_identityd_master_id" });
        //     this.AddIdentityPrincipal("aziot-edged", this.config[Service.Edged].Uid);
        //     this.AddAuthPrincipal(
        //         Service.Keyd,
        //         "aziot-edged",
        //         this.config[Service.Edged].Uid,
        //         new string[] { "iotedge_master_encryption_id", "aziot-edged-ca" });
        //     this.AddAuthPrincipal(
        //         Service.Certd,
        //         "aziot-edged",
        //         this.config[Service.Edged].Uid,
        //         new string[] { "aziot-edged/module/*" });
        // }

        public void SetManualSasProvisioning(
            string hubHostname,
            Option<string> parentHostname,
            string deviceId,
            string key)
        {
            // string keyName = DaemonConfiguration.SanitizeName(deviceId);
            // this.CreatePreloadedKey(keyName, key);

            this.config.Document.RemoveIfExists("provisioning");
            parentHostname.ForEach(parent_hostame => this.SetParentHostname(parent_hostame));
            this.config.Document.ReplaceOrAdd("provisioning.source", "manual");
            this.config.Document.ReplaceOrAdd("provisioning.iothub_hostname", hubHostname);
            this.config.Document.ReplaceOrAdd("provisioning.device_id", deviceId);
            this.config.Document.ReplaceOrAdd("provisioning.authentication.method", "sas");
            this.config.Document.ReplaceOrAdd("provisioning.authentication.device_id_pk.value", key);

            this.config.Document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");

            // this.SetAuth(keyName);
        }

        public void SetImageGarbageCollection(int minutesUntilCleanup)
        {
            this.config.Document.ReplaceOrAdd("image_garbage_collection.enabled", true);
            this.config.Document.ReplaceOrAdd("image_garbage_collection.cleanup_recurrence", "1d");
            this.config.Document.ReplaceOrAdd("image_garbage_collection.image_age_cleanup_threshold", "10s");
            string cleanupTime = DateTime.Now.Add(new TimeSpan(0, 0, minutesUntilCleanup, 0)).ToString("HH:mm");
            this.config.Document.ReplaceOrAdd("image_garbage_collection.cleanup_time", cleanupTime);
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

            this.config.Document.RemoveIfExists("provisioning");
            parentHostname.ForEach(parent_hostame => this.SetParentHostname(parent_hostame));
            this.config.Document.ReplaceOrAdd("provisioning.source", "manual");
            this.config.Document.ReplaceOrAdd("provisioning.iothub_hostname", hubhostname);
            this.config.Document.ReplaceOrAdd("provisioning.device_id", deviceId);

            this.config.Document.ReplaceOrAdd("provisioning.authentication.method", "x509");

            // string certFileName = Path.GetFileName(identityCertPath);
            // string certName = DaemonConfiguration.SanitizeName(certFileName);
            this.config.Document.ReplaceOrAdd("provisioning.authentication.identity_cert", "file://" + identityCertPath);
            // this.config.Document.ReplaceOrAdd($"preloaded_certs.{certName}", "file://" + identityCertPath);

            // string keyFileName = Path.GetFileName(identityPkPath);
            // string keyName = DaemonConfiguration.SanitizeName(keyFileName);
            this.config.Document.ReplaceOrAdd("provisioning.authentication.identity_pk", "file://" + identityPkPath);
            // this.config.Document.ReplaceOrAdd($"preloaded_keys.{keyName}", "file://" + identityPkPath);

            this.config.Document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");

            // this.SetAuth(keyName);
        }

        public void SetDpsSymmetricKey(string idScope, string registrationId, string deviceKey)
        {
            this.SetBasicDpsParam(idScope);
            this.config.Document.ReplaceOrAdd("provisioning.attestation.method", "symmetric_key");
            this.config.Document.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);

            // string keyName = DaemonConfiguration.SanitizeName($"dps-symmetric-key-{registrationId}");
            // this.CreatePreloadedKey(keyName, deviceKey);
            this.config.Document.ReplaceOrAdd("provisioning.attestation.symmetric_key.value", deviceKey);

            // this.SetAuth(keyName);
        }

        public void SetDpsX509(string idScope, string registrationId, string identityCertPath, string identityPkPath)
        {
            if (!File.Exists(identityCertPath))
            {
                throw new InvalidOperationException($"{identityCertPath} does not exist");
            }

            if (!File.Exists(identityPkPath))
            {
                throw new InvalidOperationException($"{identityPkPath} does not exist");
            }

            this.SetBasicDpsParam(idScope);
            this.config.Document.ReplaceOrAdd("provisioning.attestation.method", "x509");
            // this.config.Document.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);

            // string certFileName = Path.GetFileName(identityCertPath);
            // string certName = DaemonConfiguration.SanitizeName(certFileName);
            this.config.Document.ReplaceOrAdd("provisioning.attestation.identity_cert", "file://" + identityCertPath);
            // this.config.Document.ReplaceOrAdd($"preloaded_certs.{certName}", "file://" + identityCertPath);

            // string keyFileName = Path.GetFileName(identityPkPath);
            // string keyName = DaemonConfiguration.SanitizeName(keyFileName);
            this.config.Document.ReplaceOrAdd("provisioning.attestation.identity_pk", "file://" + identityPkPath);
            // this.config.Document.ReplaceOrAdd($"preloaded_keys.{keyName}", "file://" + identityPkPath);

            // this.SetAuth(keyName);
        }

        public void SetEdgeAgentImage(string value, IEnumerable<Registry> registries)
        {
            this.config.Document.ReplaceOrAdd("agent.name", "edgeAgent");
            this.config.Document.ReplaceOrAdd("agent.type", "docker");
            this.config.Document.ReplaceOrAdd("agent.config.image", value);

            // Currently, the only place for registries is [agent.config.auth]
            // So only one registry is supported.
            if (registries.Count() > 1)
            {
                throw new ArgumentException("Currently, up to a single registry is supported");
            }

            foreach (Registry registry in registries)
            {
                this.config.Document.ReplaceOrAdd("agent.config.auth.serveraddress", registry.Address);
                this.config.Document.ReplaceOrAdd("agent.config.auth.username", registry.Username);
                this.config.Document.ReplaceOrAdd("agent.config.auth.password", registry.Password);
            }
        }

        public void SetDeviceHostname(string value)
        {
            this.config.Document.ReplaceOrAdd("hostname", value);
        }

        public void SetParentHostname(string value)
        {
            this.config.Document.ReplaceOrAdd("parent_hostname", value);
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

            // this.config.Document.ReplaceOrAdd("preloaded_certs.aziot-edged-ca", "file://" + certs.CertificatePath);
            // this.config.Document.ReplaceOrAdd("preloaded_keys.aziot-edged-ca", "file://" + certs.KeyPath);
            // this.config.Document.ReplaceOrAdd("preloaded_certs.aziot-edged-trust-bundle", "file://" + certs.TrustedCertificatesPath);
            this.config.Document.ReplaceOrAdd("trust_bundle_cert", "file://" + certs.TrustedCertificatesPath);
        }

        // public void RemoveCertificates()
        // {
        //     this.config.Document.RemoveIfExists("preloaded_certs.aziot-edged-ca");
        //     this.config.Document.RemoveIfExists("preloaded_keys.aziot-edged-ca");
        //     this.config.Document.RemoveIfExists("preloaded_certs.aziot-edged-trust-bundle");
        // }

        // public void AddIdentityPrincipal(string name, uint uid, string[] type = null, Dictionary<string, string> opts = null)
        // {
        //     string path = Path.Combine(this.config[Service.Identityd].PrincipalsPath, $"{name}-principal.toml");

        //     string principal = string.Join(
        //         "\n",
        //         "[[principal]]",
        //         $"uid = {uid}",
        //         $"name = \"{name}\"");

        //     if (type != null)
        //     {
        //         // Need to quote each type.
        //         for (int i = 0; i < type.Length; i++)
        //         {
        //             type[i] = $"\"{type[i]}\"";
        //         }

        //         string types = string.Join(", ", type);
        //         principal = string.Join("\n", principal, $"idtype = [{types}]");
        //     }

        //     if (opts != null)
        //     {
        //         foreach (KeyValuePair<string, string> opt in opts)
        //         {
        //             principal = string.Join("\n", principal, $"{opt.Key} = {opt.Value}");
        //         }
        //     }

        //     File.WriteAllText(path, principal + "\n");
        //     OsPlatform.Current.SetOwner(path, this.config[Service.Identityd].Owner, "644");
        // }

        // public void AddAuthPrincipal(Service service, string name, uint uid, string[] credentials)
        // {
        //     if (credentials == null || credentials.Length == 0)
        //     {
        //         throw new ArgumentException("Empty array of credentials");
        //     }

        //     string auth = string.Empty;

        //     switch (service)
        //     {
        //         case Service.Keyd:
        //             auth += "keys = [";
        //             break;
        //         case Service.Certd:
        //             auth += "certs = [";
        //             break;
        //         default:
        //             throw new ArgumentException("Authorization is only relevant for keyd and certd");
        //     }

        //     for (int i = 0; i < credentials.Length; i++)
        //     {
        //         credentials[i] = $"\"{credentials[i]}\"";
        //     }

        //     auth += string.Join(", ", credentials);
        //     auth += "]";

        //     string path = Path.Combine(this.config[service].PrincipalsPath, $"{name}-principal.toml");

        //     string principal = string.Join(
        //         "\n",
        //         "[[principal]]",
        //         $"uid = {uid}",
        //         auth);

        //     File.WriteAllText(path, principal + "\n");
        //     OsPlatform.Current.SetOwner(path, this.config[service].Owner, "644");
        // }

        public async Task UpdateAsync(CancellationToken token)
        {
            string path = this.config.ConfigPath;
            await File.WriteAllTextAsync(path, this.config.Document.ToString());
            Serilog.Log.Information(await File.ReadAllTextAsync(path));
        }

        private static string SanitizeName(string name)
        {
            // Due to '.' being used as a delimiter for config file tables, names cannot contain '.'
            // Strip non-alphanumeric characters except for '-' for a safe name.
            return Regex.Replace(name, "[^A-Za-z0-9 -]", string.Empty);
        }

        // All names passed to this function must be sanitized with DaemonConfiguration.SanitizeName
        // private void CreatePreloadedKey(string name, string value)
        // {
        //     string filePath = Path.Combine(FixedPaths.E2E_TEST_DIR, $"{name}.key");

        //     File.WriteAllBytes(filePath, Convert.FromBase64String(value));
        //     OsPlatform.Current.SetOwner(filePath, this.config[Service.Keyd].Owner, "600");

        //     this.config[Service.Keyd].Document.ReplaceOrAdd($"preloaded_keys.{name}", "file://" + filePath);
        // }
    }
}
