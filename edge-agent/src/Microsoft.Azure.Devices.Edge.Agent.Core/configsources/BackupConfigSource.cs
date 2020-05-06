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

    public class BackupConfigSource : IConfigSource
    {
        readonly IDeploymentBackupSource backupSource;
        readonly IConfigSource underlying;
        readonly AsyncLock sync = new AsyncLock();

        Option<DeploymentConfigInfo> lastBackedUpConfig = Option.None<DeploymentConfigInfo>();

        public BackupConfigSource(IDeploymentBackupSource backupSource, IConfigSource underlying)
        {
            this.backupSource = Preconditions.CheckNotNull(backupSource, nameof(backupSource));
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
            Events.Created(this.backupSource.Name);
        }

        public IConfiguration Configuration => this.underlying.Configuration;

        public async Task<DeploymentConfigInfo> GetDeploymentConfigInfoAsync()
        {
            try
            {
                DeploymentConfigInfo deploymentConfig = await this.underlying.GetDeploymentConfigInfoAsync();
                if (deploymentConfig == DeploymentConfigInfo.Empty)
                {
                    Events.RestoringFromBackup(deploymentConfig, this.backupSource.Name);
                    deploymentConfig = await this.ReadFromBackupAsync();
                }
                else if (!deploymentConfig.Exception.HasValue)
                {
                    // TODO - Backing up the config every time for now, probably should optimize this.
                    await this.BackupDeploymentConfigAsync(deploymentConfig);
                }

                return deploymentConfig;
            }
            catch (Exception ex)
            {
                Events.RestoringFromBackup(ex, this.backupSource.Name);
                return await this.ReadFromBackupAsync();
            }
        }

        public void Dispose()
        {
            this.underlying?.Dispose();
        }

        async Task<DeploymentConfigInfo> ReadFromBackupAsync()
        {
            try
            {
                if (!this.lastBackedUpConfig.HasValue)
                {
                    using (await this.sync.LockAsync())
                    {
                        var deploymentConfigInfo = await this.backupSource.ReadFromBackupAsync();
                        this.lastBackedUpConfig = Option.Maybe(deploymentConfigInfo);
                    }
                }
            }
            catch (Exception e)
            {
                Events.GetBackupFailed(e, this.backupSource.Name);
            }

            return this.lastBackedUpConfig.GetOrElse(DeploymentConfigInfo.Empty);
        }

        async Task BackupDeploymentConfigAsync(DeploymentConfigInfo deploymentConfigInfo)
        {
            // backup the config info only if there isn't an error in it
            if (!deploymentConfigInfo.Exception.HasValue
                && !this.lastBackedUpConfig.Filter(c => deploymentConfigInfo.Equals(c)).HasValue)
            {
                using (await this.sync.LockAsync())
                {
                    this.lastBackedUpConfig = Option.Some(deploymentConfigInfo);

                    try
                    {
                        await this.backupSource.BackupDeploymentConfigAsync(deploymentConfigInfo);
                    }
                    catch (Exception e)
                    {
                        Events.SetBackupFailed(e, this.backupSource.Name);
                    }
                }
            }
        }

        static class Events
        {
            const int IdStart = AgentEventIds.BackupConfigSource;
            static readonly ILogger Log = Logger.Factory.CreateLogger<BackupConfigSource>();

            enum EventIds
            {
                Created = IdStart,
                SetBackupFailed,
                GetBackupFailed,
                RestoringFromBackup
            }

            public static void Created(string backupSource)
            {
                Log.LogDebug((int)EventIds.Created, $"Edge agent config backup created here - {backupSource}");
            }

            public static void SetBackupFailed(Exception exception, string backupSource)
            {
                Log.LogError((int)EventIds.SetBackupFailed, exception, $"Error backing up edge agent config to {backupSource}");
            }

            public static void GetBackupFailed(Exception exception, string backupSource)
            {
                Log.LogError((int)EventIds.GetBackupFailed, exception, $"Failed to read edge agent config from backup {backupSource}");
            }

            public static void RestoringFromBackup(Exception exception, string backupSource)
            {
                Log.LogWarning((int)EventIds.RestoringFromBackup, exception, $"Error getting edge agent config. Attempting to read config from backup ({backupSource}) instead");
            }

            public static void RestoringFromBackup(DeploymentConfigInfo deploymentConfig, string backupSource)
            {
                string reason = deploymentConfig.Exception.Map(e => $"Error getting edge agent config - {e}")
                    .GetOrElse("Empty edge agent config was received");
                Log.LogWarning((int)EventIds.RestoringFromBackup, $"{reason}. Attempting to read config from backup ({backupSource}) instead");
            }
        }
    }
}
