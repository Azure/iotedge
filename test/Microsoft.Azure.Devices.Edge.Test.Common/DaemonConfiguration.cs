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
        const string GlobalEndPoint = "https://global.azure-devices-provisioning.net";
        TomlDocument document;
        string path;

        public DaemonConfiguration(string superTomlPath)
        {
            Directory.CreateDirectory(Directory.GetParent(superTomlPath).FullName);
            string contents = File.Exists(superTomlPath) ? File.ReadAllText(superTomlPath) : string.Empty;
            this.document = new TomlDocument(contents);
            this.path = superTomlPath;
        }

        public void AddHttpsProxy(Uri proxy)
        {
            this.document.ReplaceOrAdd("agent.env.https_proxy", proxy.ToString());
            // The config file is configured during test suite initialization, before we know which
            // protocol a given test will use. Always use AmqpWs, and when each test deploys a
            // configuration, it can update the config to use whatever it wants.
            this.document.ReplaceOrAdd("agent.env.UpstreamProtocol", "AmqpWs");
        }

        public void AddAgentUserId(string uid)
        {
            this.document.ReplaceOrAdd("agent.env.EDGEAGENTUSER_ID", uid);
        }

        void SetBasicDpsParam(string idScope)
        {
            this.document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");

            this.document.RemoveIfExists("provisioning");
            this.document.ReplaceOrAdd("provisioning.source", "dps");
            this.document.ReplaceOrAdd("provisioning.global_endpoint", GlobalEndPoint);
            this.document.ReplaceOrAdd("provisioning.id_scope", idScope);
        }

        public void SetManualSasProvisioning(
            string hubHostname,
            Option<string> parentHostname,
            string deviceId,
            string key)
        {
            this.document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");
            parentHostname.ForEach(parent_hostame => this.SetParentHostname(parent_hostame));

            this.document.RemoveIfExists("provisioning");
            this.document.ReplaceOrAdd("provisioning.source", "manual");
            this.document.ReplaceOrAdd("provisioning.iothub_hostname", hubHostname);
            this.document.ReplaceOrAdd("provisioning.device_id", deviceId);
            this.document.ReplaceOrAdd("provisioning.authentication.method", "sas");
            this.document.ReplaceOrAdd("provisioning.authentication.device_id_pk.value", key);
        }

        public void SetImageGarbageCollection(int minutesUntilCleanup)
        {
            this.document.ReplaceOrAdd("image_garbage_collection.enabled", true);
            this.document.ReplaceOrAdd("image_garbage_collection.cleanup_recurrence", "1d");
            this.document.ReplaceOrAdd("image_garbage_collection.image_age_cleanup_threshold", "10s");
            string cleanupTime = DateTime.Now.Add(new TimeSpan(0, 0, minutesUntilCleanup, 0)).ToString("HH:mm");
            this.document.ReplaceOrAdd("image_garbage_collection.cleanup_time", cleanupTime);
        }

        public void SetDeviceManualX509(
            string hubhostname,
            Option<string> parentHostname,
            string deviceId,
            string identityCertPath,
            string identityPkPath)
        {
            if (!File.Exists(identityCertPath))
            {
                throw new InvalidOperationException($"{identityCertPath} does not exist");
            }

            if (!File.Exists(identityPkPath))
            {
                throw new InvalidOperationException($"{identityPkPath} does not exist");
            }

            this.document.ReplaceOrAdd("auto_reprovisioning_mode", "AlwaysOnStartup");
            parentHostname.ForEach(parent_hostame => this.SetParentHostname(parent_hostame));

            this.document.RemoveIfExists("provisioning");
            this.document.ReplaceOrAdd("provisioning.source", "manual");
            this.document.ReplaceOrAdd("provisioning.iothub_hostname", hubhostname);
            this.document.ReplaceOrAdd("provisioning.device_id", deviceId);
            this.document.ReplaceOrAdd("provisioning.authentication.method", "x509");
            this.document.ReplaceOrAdd("provisioning.authentication.identity_cert", "file://" + identityCertPath);
            this.document.ReplaceOrAdd("provisioning.authentication.identity_pk", "file://" + identityPkPath);
        }

        public void SetDpsSymmetricKey(string idScope, string registrationId, string deviceKey)
        {
            this.SetBasicDpsParam(idScope);
            this.document.ReplaceOrAdd("provisioning.attestation.method", "symmetric_key");
            this.document.ReplaceOrAdd("provisioning.attestation.registration_id", registrationId);
            this.document.ReplaceOrAdd("provisioning.attestation.symmetric_key.value", deviceKey);
        }

        public void SetDpsX509(string idScope, string identityCertPath, string identityPkPath)
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
            this.document.ReplaceOrAdd("provisioning.attestation.method", "x509");
            this.document.ReplaceOrAdd("provisioning.attestation.identity_cert", "file://" + identityCertPath);
            this.document.ReplaceOrAdd("provisioning.attestation.identity_pk", "file://" + identityPkPath);
        }

        public void SetEdgeAgentImage(string value, IEnumerable<Registry> registries)
        {
            this.document.ReplaceOrAdd("agent.name", "edgeAgent");
            this.document.ReplaceOrAdd("agent.type", "docker");
            this.document.ReplaceOrAdd("agent.config.image", value);

            // Currently, the only place for registries is [agent.config.auth]
            // So only one registry is supported.
            if (registries.Count() > 1)
            {
                throw new ArgumentException("Currently, up to a single registry is supported");
            }

            foreach (Registry registry in registries)
            {
                this.document.ReplaceOrAdd("agent.config.auth.serveraddress", registry.Address);
                this.document.ReplaceOrAdd("agent.config.auth.username", registry.Username);
                this.document.ReplaceOrAdd("agent.config.auth.password", registry.Password);
            }
        }

        public void SetDeviceHostname(string value)
        {
            this.document.ReplaceOrAdd("hostname", value);
        }

        public void SetDeviceHomedir(string value)
        {
            this.document.ReplaceOrAdd("homedir", value);
        }

        public void SetParentHostname(string value)
        {
            this.document.ReplaceOrAdd("parent_hostname", value);
        }

        public void SetMobyRuntimeUri(string value)
        {
            this.document.ReplaceOrAdd("moby_runtime.uri", value);
            this.document.ReplaceOrAdd("moby_runtime.network", "azure-iot-edge");
        }

        public void SetConnectSockets(string workloadUri, string managementUri)
        {
            this.document.ReplaceOrAdd("connect.workload_uri", workloadUri);
            this.document.ReplaceOrAdd("connect.management_uri", managementUri);
        }

        public void SetListenSockets(string workloadUri, string managementUri)
        {
            this.document.ReplaceOrAdd("listen.workload_uri", workloadUri);
            this.document.ReplaceOrAdd("listen.management_uri", managementUri);
        }

        public void SetCertificates(CaCertificates certs)
        {
            this.document.ReplaceOrAdd("edge_ca.cert", "file://" + certs.CertificatePath);
            this.document.ReplaceOrAdd("edge_ca.pk", "file://" + certs.KeyPath);
            this.document.ReplaceOrAdd("trust_bundle_cert", "file://" + certs.TrustedCertificatesPath);
        }

        public async Task UpdateAsync(CancellationToken token)
        {
            await File.WriteAllTextAsync(this.path, this.document.ToString());
        }
    }
}
