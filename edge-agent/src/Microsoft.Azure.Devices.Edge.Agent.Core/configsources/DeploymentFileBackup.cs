// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class DeploymentFileBackup : IDeploymentBackupSource
    {
        readonly ISerde<DeploymentConfigInfo> serde;
        readonly IEncryptionProvider encryptionProvider;

        Option<DeploymentConfigInfo> lastBackedUpConfig = Option.None<DeploymentConfigInfo>();

        public DeploymentFileBackup(string path, ISerde<DeploymentConfigInfo> serde, IEncryptionProvider encryptionProvider)
        {
            this.Name = Preconditions.CheckNonWhiteSpace(path, nameof(path));
            this.serde = Preconditions.CheckNotNull(serde, nameof(serde));
            this.encryptionProvider = Preconditions.CheckNotNull(encryptionProvider, nameof(IEncryptionProvider));
        }

        public string Name { get; }

        public async Task BackupDeploymentConfigAsync(DeploymentConfigInfo deploymentConfigInfo)
        {
            try
            {
                // backup the config info only if there isn't an error in it
                if (!deploymentConfigInfo.Exception.HasValue)
                {
                    string json = this.serde.Serialize(deploymentConfigInfo);
                    string encrypted = await this.encryptionProvider.EncryptAsync(json);
                    await DiskFile.WriteAllAsync(this.Name, encrypted);
                }
            }
            catch (Exception e)
            {
                Events.SetBackupFailed(e, this.Name);
            }
        }

        public async Task<DeploymentConfigInfo> ReadFromBackupAsync()
        {
            if (!File.Exists(this.Name))
            {
                Events.BackupFileDoesNotExist(this.Name);
                return DeploymentConfigInfo.Empty;
            }
            else
            {
                string encryptedJson = await DiskFile.ReadAllAsync(this.Name);
                string json = await this.encryptionProvider.DecryptAsync(encryptedJson);
                DeploymentConfigInfo deploymentConfigInfo = this.serde.Deserialize(json);
                Events.ObtainedDeploymentFromBackup(this.Name);
                this.lastBackedUpConfig = Option.Some(deploymentConfigInfo);
                return deploymentConfigInfo;
            }
        }

        static class Events
        {
            const int IdStart = AgentEventIds.DeploymentFileBackup;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeploymentFileBackup>();

            enum EventIds
            {
                Created = IdStart,
                SetBackupFailed,
                RestoringFromBackup
            }

            public static void Created(string filename)
            {
                Log.LogDebug((int)EventIds.Created, $"Edge agent config backup created here - {filename}");
            }

            public static void SetBackupFailed(Exception exception, string filename)
            {
                Log.LogError((int)EventIds.SetBackupFailed, exception, $"Error backing up edge agent config to {filename}");
            }

            public static void RestoringFromBackup(Exception exception, string filename)
            {
                Log.LogWarning((int)EventIds.RestoringFromBackup, exception, $"Error getting edge agent config. Attempting to read config from backup file ({filename}) instead");
            }

            public static void BackupFileDoesNotExist(string filename)
            {
                Log.LogInformation((int)EventIds.Created, $"Edge agent config backup file does not exist - {filename}");
            }

            public static void ObtainedDeploymentFromBackup(string filename)
            {
                Log.LogInformation((int)EventIds.Created, $"Obtained edge agent config from backup config file - {filename}");
            }
        }
    }
}
