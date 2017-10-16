// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class FileBackupConfigSource : IConfigSource
    {
        readonly string configFilePath;
        readonly IConfigSource underlying;
        readonly AsyncLock sync = new AsyncLock();

        public FileBackupConfigSource(string path, IConfigSource underlying)
        {
            this.configFilePath = Preconditions.CheckNonWhiteSpace(path, nameof(path));
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
            Events.Created(this.configFilePath);
        }

        public IConfiguration Configuration => this.underlying.Configuration;

        async Task<AgentConfig> ReadFromBackup()
        {
            try
            {
                using (await this.sync.LockAsync())
                {
                    string json = await DiskFile.ReadAllAsync(this.configFilePath);
                    return json.FromJson<AgentConfig>();
                }
            }
            catch (Exception e)
            {
                Events.GetBackupFailed(e, this.configFilePath);
                throw;
            }
        }

        async Task BackupDeploymentConfig(AgentConfig deploymentConfig)
        {
            try
            {
                string json = deploymentConfig.ToJson();
                using (await this.sync.LockAsync())
                {
                    await DiskFile.WriteAllAsync(this.configFilePath, json);
                }
            }
            catch (Exception e)
            {
                Events.SetBackupFailed(e, this.configFilePath);
                throw new FileBackupException($"Failed to backup config at {this.configFilePath}", e);
            }
        }

        public async Task<AgentConfig> GetAgentConfigAsync()
        {
            try
            {
                AgentConfig deploymentConfig = await this.underlying.GetAgentConfigAsync();
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
                Log.LogInformation((int)EventIds.Created, $"FileBackupConfigSource created with filename {filename}");
            }

            public static void SetBackupFailed(Exception exception, string filename)
            {
                Log.LogError((int)EventIds.SetBackupFailed, exception, $"FileBackupConfigSource failed saving backup module set to {filename}");
            }

            public static void GetBackupFailed(Exception exception, string filename)
            {
                Log.LogError((int)EventIds.GetBackupFailed, exception, $"FileBackupConfigSource failed getting backup module set from {filename}");
            }

            public static void RestoringFromBackup(Exception exception, string filename)
            {
                Log.LogWarning((int)EventIds.RestoringFromBackup, exception, $"FileBackupConfigSource using backup module set from {filename}");
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
