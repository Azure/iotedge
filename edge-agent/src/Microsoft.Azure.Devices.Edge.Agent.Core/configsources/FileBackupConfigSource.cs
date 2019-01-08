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

    public class FileBackupConfigSource : IConfigSource
    {
        readonly string configFilePath;
        readonly IConfigSource underlying;
        readonly AsyncLock sync = new AsyncLock();
        readonly ISerde<DeploymentConfigInfo> serde;
        readonly IEncryptionProvider encryptionProvider;

        Option<DeploymentConfigInfo> lastBackedUpConfig = Option.None<DeploymentConfigInfo>();

        public FileBackupConfigSource(string path, IConfigSource underlying, ISerde<DeploymentConfigInfo> serde, IEncryptionProvider encryptionProvider)
        {
            this.configFilePath = Preconditions.CheckNonWhiteSpace(path, nameof(path));
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
            this.serde = Preconditions.CheckNotNull(serde, nameof(serde));
            this.encryptionProvider = Preconditions.CheckNotNull(encryptionProvider, nameof(IEncryptionProvider));
            Events.Created(this.configFilePath);
        }

        public IConfiguration Configuration => this.underlying.Configuration;

        public async Task<DeploymentConfigInfo> GetDeploymentConfigInfoAsync()
        {
            try
            {
                DeploymentConfigInfo deploymentConfig = await this.underlying.GetDeploymentConfigInfoAsync();
                if (deploymentConfig == DeploymentConfigInfo.Empty)
                {
                    Events.RestoringFromBackup(deploymentConfig, this.configFilePath);
                    deploymentConfig = await this.ReadFromBackup();
                }
                else if (!deploymentConfig.Exception.HasValue)
                {
                    // TODO - Backing up the config every time for now, probably should optimize this.
                    await this.BackupDeploymentConfig(deploymentConfig);
                }

                return deploymentConfig;
            }
            catch (Exception ex)
            {
                Events.RestoringFromBackup(ex, this.configFilePath);
                return await this.ReadFromBackup();
            }
        }

        public void Dispose()
        {
            this.underlying?.Dispose();
        }

        async Task<DeploymentConfigInfo> ReadFromBackup()
        {
            DeploymentConfigInfo backedUpDeploymentConfigInfo = DeploymentConfigInfo.Empty;

            try
            {
                backedUpDeploymentConfigInfo = await this.lastBackedUpConfig
                    .Map(v => Task.FromResult(v))
                    .GetOrElse(
                        async () =>
                        {
                            if (!File.Exists(this.configFilePath))
                            {
                                Events.BackupFileDoesNotExist(this.configFilePath);
                                return DeploymentConfigInfo.Empty;
                            }
                            else
                            {
                                using (await this.sync.LockAsync())
                                {
                                    string encryptedJson = await DiskFile.ReadAllAsync(this.configFilePath);
                                    string json = await this.encryptionProvider.DecryptAsync(encryptedJson);
                                    DeploymentConfigInfo deploymentConfigInfo = this.serde.Deserialize(json);
                                    Events.ObtainedDeploymentFromBackup(this.configFilePath);
                                    this.lastBackedUpConfig = Option.Some(deploymentConfigInfo);
                                    return deploymentConfigInfo;
                                }
                            }
                        });
            }
            catch (Exception e)
            {
                Events.GetBackupFailed(e, this.configFilePath);
            }

            return backedUpDeploymentConfigInfo;
        }

        async Task BackupDeploymentConfig(DeploymentConfigInfo deploymentConfigInfo)
        {
            try
            {
                // backup the config info only if there isn't an error in it
                if (!deploymentConfigInfo.Exception.HasValue
                    && !this.lastBackedUpConfig.Filter(c => deploymentConfigInfo.Equals(c)).HasValue)
                {
                    string json = this.serde.Serialize(deploymentConfigInfo);
                    string encrypted = await this.encryptionProvider.EncryptAsync(json);
                    using (await this.sync.LockAsync())
                    {
                        await DiskFile.WriteAllAsync(this.configFilePath, encrypted);
                        this.lastBackedUpConfig = Option.Some(deploymentConfigInfo);
                    }
                }
            }
            catch (Exception e)
            {
                Events.SetBackupFailed(e, this.configFilePath);
            }
        }

        static class Events
        {
            const int IdStart = AgentEventIds.FileBackupConfigSource;
            static readonly ILogger Log = Logger.Factory.CreateLogger<FileBackupConfigSource>();

            enum EventIds
            {
                Created = IdStart,
                SetBackupFailed,
                GetBackupFailed,
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

            public static void GetBackupFailed(Exception exception, string filename)
            {
                Log.LogError((int)EventIds.GetBackupFailed, exception, $"Failed to read edge agent config from backup file {filename}");
            }

            public static void RestoringFromBackup(Exception exception, string filename)
            {
                Log.LogWarning((int)EventIds.RestoringFromBackup, exception, $"Error getting edge agent config. Attempting to read config from backup file ({filename}) instead");
            }

            public static void RestoringFromBackup(DeploymentConfigInfo deploymentConfig, string filename)
            {
                string reason = deploymentConfig.Exception.Map(e => $"Error getting edge agent config - {e}")
                    .GetOrElse("Empty edge agent config was received");
                Log.LogWarning((int)EventIds.RestoringFromBackup, $"{reason}. Attempting to read config from backup file ({filename}) instead");
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
