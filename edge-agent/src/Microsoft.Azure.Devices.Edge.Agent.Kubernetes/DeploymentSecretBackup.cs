// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;

    public class DeploymentSecretBackup : IDeploymentBackupSource
    {
        const string BackupData = "backup.json";
        readonly string deviceNamespace;
        readonly KubernetesModuleOwner moduleOwner;
        readonly ISerde<DeploymentConfigInfo> serde;
        readonly IKubernetes client;

        public DeploymentSecretBackup(string secretName, string deviceNamespace, KubernetesModuleOwner moduleOwner, ISerde<DeploymentConfigInfo> serde, IKubernetes client)
        {
            this.Name = Preconditions.CheckNonWhiteSpace(secretName, nameof(secretName));
            this.deviceNamespace = Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace));
            this.moduleOwner = Preconditions.CheckNotNull(moduleOwner, nameof(moduleOwner));
            this.serde = Preconditions.CheckNotNull(serde, nameof(serde));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
        }

        public string Name { get; }

        public async Task<DeploymentConfigInfo> ReadFromBackupAsync()
        {
            Option<DeploymentConfigInfo> config = Option.None<DeploymentConfigInfo>();

            try
            {
                V1Secret backupSecret = await this.client.ReadNamespacedSecretAsync(this.Name, this.deviceNamespace);
                config = Option.Maybe(backupSecret)
                            .FlatMap(
                               s =>
                               {
                                   if (s.Data != null && s.Data.TryGetValue(BackupData, out byte[] backupconfig))
                                   {
                                       string backupJson = System.Text.Encoding.UTF8.GetString(s.Data[BackupData]);
                                       DeploymentConfigInfo deploymentConfigInfo = this.serde.Deserialize(backupJson);
                                       Events.ObtainedDeploymentFromBackup(this.Name);
                                       return Option.Maybe(deploymentConfigInfo);
                                   }

                                   return Option.None<DeploymentConfigInfo>();
                               });
            }
            catch (Exception e)
            {
                Events.GetBackupFailed(e, this.Name);
            }

            return config.GetOrElse(DeploymentConfigInfo.Empty);
        }

        public async Task BackupDeploymentConfigAsync(DeploymentConfigInfo deploymentConfigInfo)
        {
            try
            {
                // backup the config info only if there isn't an error in it
                if (!deploymentConfigInfo.Exception.HasValue)
                {
                    byte[] json = System.Text.Encoding.UTF8.GetBytes(this.serde.Serialize(deploymentConfigInfo));

                    var secretMeta = new V1ObjectMeta(
                        name: this.Name,
                        namespaceProperty: this.deviceNamespace,
                        ownerReferences: this.moduleOwner.ToOwnerReferences());
                    var secretData = new Dictionary<string, byte[]> { [BackupData] = json };
                    var newSecret = new V1Secret("v1", secretData, type: Constants.K8sBackupSecretType, kind: "Secret", metadata: secretMeta);

                    Option<V1Secret> currentSecret;
                    try
                    {
                        currentSecret = Option.Maybe(await this.client.ReadNamespacedSecretAsync(this.Name, this.deviceNamespace));
                    }
                    catch (HttpOperationException ex) when (!ex.IsFatal())
                    {
                        currentSecret = Option.None<V1Secret>();
                    }

                    var v1Secret = await currentSecret.Match(
                        async s =>
                        {
                            if (s.Data != null && s.Data.TryGetValue(BackupData, out byte[] backupSecretData) &&
                                backupSecretData.SequenceEqual(json))
                            {
                                return s;
                            }

                            return await this.client.ReplaceNamespacedSecretAsync(
                                newSecret,
                                this.Name,
                                this.deviceNamespace);
                        },
                        async () => await this.client.CreateNamespacedSecretAsync(newSecret, this.deviceNamespace));
                    if (v1Secret == null)
                    {
                        throw new InvalidBackupException("backup secret was not properly created");
                    }
                }
            }
            catch (Exception e)
            {
                Events.SetBackupFailed(e, this.Name);
            }
        }

        static class Events
        {
            const int IdStart = KubernetesEventIds.SecretBackup;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeploymentSecretBackup>();

            enum EventIds
            {
                Created = IdStart,
                SetBackupFailed,
                GetBackupFailed
            }

            public static void SetBackupFailed(Exception exception, string secretName)
            {
                Log.LogError((int)EventIds.SetBackupFailed, exception, $"Error backing up edge agent config to secret {secretName}");
            }

            public static void GetBackupFailed(Exception exception, string secretName)
            {
                Log.LogError((int)EventIds.GetBackupFailed, exception, $"Failed to read edge agent config from backup secret {secretName}");
            }

            public static void ObtainedDeploymentFromBackup(string secretName)
            {
                Log.LogInformation((int)EventIds.Created, $"Obtained edge agent config from backup config secret - {secretName}");
            }
        }
    }
}
