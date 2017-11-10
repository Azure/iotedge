// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;
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

        public FileBackupConfigSource(string path, IConfigSource underlying, ISerde<DeploymentConfigInfo> serde)
        {
            this.configFilePath = Preconditions.CheckNonWhiteSpace(path, nameof(path));
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
            this.serde = Preconditions.CheckNotNull(serde, nameof(serde));
            Events.Created(this.configFilePath);
        }

        public IConfiguration Configuration => this.underlying.Configuration;

        async Task<DeploymentConfigInfo> ReadFromBackup()
        {
            try
            {
                using (await this.sync.LockAsync())
                {
                    string json = await DiskFile.ReadAllAsync(this.configFilePath);
                    return this.serde.Deserialize(json);
                }
            }
            catch (Exception e)
            {
                Events.GetBackupFailed(e, this.configFilePath);
                throw;
            }
        }

        async Task BackupDeploymentConfig(DeploymentConfigInfo deploymentConfigInfo)
        {
            try
            {
                // backup the config info only if there isn't an error in it
                if (deploymentConfigInfo.Exception.HasValue == false)
                {
                    string json = this.serde.Serialize(deploymentConfigInfo);
                    using (await this.sync.LockAsync())
                    {
                        await DiskFile.WriteAllAsync(this.configFilePath, json);
                    }
                }
            }
            catch (Exception e)
            {
                Events.SetBackupFailed(e, this.configFilePath);
                throw new FileBackupException($"Failed to backup config at {this.configFilePath}", e);
            }
        }

        public async Task<DeploymentConfigInfo> GetDeploymentConfigInfoAsync()
        {
            try
            {
                DeploymentConfigInfo deploymentConfig = await this.underlying.GetDeploymentConfigInfoAsync();
                // TODO - Backing up the config every time for now, probably should optimize this.
                await this.BackupDeploymentConfig(deploymentConfig);
                return deploymentConfig;
            }
            catch (FileBackupException)
            {
                // throw a custom exception when file back up fails so as to not be caught
                // by the generic exception handler and not attempt to read from the backup
                throw;
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

        static class Events
        {
            const int IdStart = AgentEventIds.FileBackupConfigSource;
            static readonly ILogger Log = Logger.Factory.CreateLogger<FileBackupConfigSource>();

            public static void Created(string filename)
            {
                Log.LogInformation((int)EventIds.Created, $"Edge agent config backup created here - {filename}");
            }

            public static void SetBackupFailed(Exception exception, string filename)
            {
                Log.LogError((int)EventIds.SetBackupFailed, exception, $"Error backing up edge agent config to {filename}");
            }

            public static void GetBackupFailed(Exception exception, string filename)
            {
                Log.LogError((int)EventIds.GetBackupFailed, exception, $"Failed to read edge agent config from file {filename}");
            }

            public static void RestoringFromBackup(Exception exception, string filename)
            {
                Log.LogWarning((int)EventIds.RestoringFromBackup, exception, $"Error getting edge agent config. Reading config from backup ({filename}) instead");
            }

            enum EventIds
            {
                Created = IdStart,
                SetBackupFailed,
                GetBackupFailed,
                RestoringFromBackup
            }
        }
    }
}
