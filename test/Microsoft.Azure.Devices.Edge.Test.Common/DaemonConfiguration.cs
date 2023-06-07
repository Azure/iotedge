// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DaemonConfiguration
    {
        struct Config
        {
            public string ConfigPath;
            public TomlDocument Document;
        }

        const string GlobalEndPoint = "https://global.azure-devices-provisioning.net";
        Config config;

        public DaemonConfiguration(string superTomlPath)
        {
            Directory.CreateDirectory(Directory.GetParent(superTomlPath).FullName);
            string contents = File.Exists(superTomlPath) ? File.ReadAllText(superTomlPath) : string.Empty;
            this.config = new Config
            {
                ConfigPath = superTomlPath,
                Document = new TomlDocument(contents)
            };
        }

        public void AddHttpsProxy(Uri proxy)
        {
            this.config.Document.ReplaceOrAdd("agent.env.https_proxy", proxy.ToString());
            // The config file is configured during test suite initialization, before we know which
            // protocol a given test will use. Always use AmqpWs, and when each test deploys a
            // configuration, it can update the config to use whatever it wants.
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

        public void SetManualSasProvisioning(
            string hubHostname,
            Option<string> parentHostname,
            string deviceId,
            string key)
        {
            this.config.Document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");

            this.config.Document.RemoveIfExists("provisioning");
            parentHostname.ForEach(parent_hostame => this.SetParentHostname(parent_hostame));
            this.config.Document.ReplaceOrAdd("provisioning.source", "manual");
            this.config.Document.ReplaceOrAdd("provisioning.iothub_hostname", hubHostname);
            this.config.Document.ReplaceOrAdd("provisioning.device_id", deviceId);
            this.config.Document.ReplaceOrAdd("provisioning.authentication.method", "sas");
            this.config.Document.ReplaceOrAdd("provisioning.authentication.device_id_pk.value", key);
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

            this.config.Document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");

            this.config.Document.RemoveIfExists("provisioning");
            parentHostname.ForEach(parent_hostame => this.SetParentHostname(parent_hostame));
            this.config.Document.ReplaceOrAdd("provisioning.source", "manual");
            this.config.Document.ReplaceOrAdd("provisioning.iothub_hostname", hubhostname);
            this.config.Document.ReplaceOrAdd("provisioning.device_id", deviceId);
            this.config.Document.ReplaceOrAdd("provisioning.authentication.method", "x509");
            this.config.Document.ReplaceOrAdd("provisioning.authentication.identity_cert", "file://" + identityCertPath);
            this.config.Document.ReplaceOrAdd("provisioning.authentication.identity_pk", "file://" + identityPkPath);
        }

        public void SetDpsSymmetricKey(string idScope, string registrationId, string deviceKey)
        {
            this.SetBasicDpsParam(idScope);
            this.config.Document.ReplaceOrAdd("provisioning.attestation.method", "symmetric_key");
            this.config.Document.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);
            this.config.Document.ReplaceOrAdd("provisioning.attestation.symmetric_key.value", deviceKey);
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
            this.config.Document.ReplaceOrAdd("provisioning.attestation.identity_cert", "file://" + identityCertPath);
            this.config.Document.ReplaceOrAdd("provisioning.attestation.identity_pk", "file://" + identityPkPath);
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

            this.config.Document.ReplaceOrAdd("edge_ca.cert", "file://" + certs.CertificatePath);
            this.config.Document.ReplaceOrAdd("edge_ca.pk", "file://" + certs.KeyPath);
            this.config.Document.ReplaceOrAdd("trust_bundle_cert", "file://" + certs.TrustedCertificatesPath);
        }

        public async Task UpdateAsync(CancellationToken token)
        {
            string path = this.config.ConfigPath;
            await File.WriteAllTextAsync(path, this.config.Document.ToString());
            // Serilog.Log.Information(await File.ReadAllTextAsync(path));
        }
    }
}
