// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    public class FileBackupConfigSource : BaseConfigSource
    {
        readonly string configFilePath;
        readonly ISerde<ModuleSet> moduleSetSerde;
        readonly IConfigSource underlying;
        readonly AsyncLock sync;

        public FileBackupConfigSource(string path, ISerde<ModuleSet> moduleSetSerde, IConfigSource underlying, IConfiguration configuration) : base(configuration)
        {
            this.configFilePath = Preconditions.CheckNonWhiteSpace(path, nameof(path));
            this.moduleSetSerde = Preconditions.CheckNotNull(moduleSetSerde, nameof(moduleSetSerde));
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
            Preconditions.CheckNotNull(configuration, nameof(configuration));

            this.sync = new AsyncLock();

            // subscribe only to the module set changed event and swallow the module set failed event 
            // as we will generate the latter from appropriate locations in this class
            this.underlying.ModuleSetChanged += async (sender, diff) => await this.OnModuleSetChanged(diff);

            Events.Created(configFilePath);
        }

        public override async Task<ModuleSet> GetModuleSetAsync()
        {
            try
            {
                ModuleSet updated = await this.underlying.GetModuleSetAsync();
                await this.BackupModuleSet(updated);
                return updated;
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

        public override event EventHandler<Diff> ModuleSetChanged;

        public override event EventHandler<Exception> ModuleSetFailed;

        public override void Dispose()
        {
        }

        private async Task<ModuleSet> ReadFromBackup()
        {
            try
            {
                using (await this.sync.LockAsync())
                {
                    string json = await DiskFile.ReadAllAsync(this.configFilePath);
                    return this.moduleSetSerde.Deserialize(json);
                }
            }
            catch (Exception e)
            {
                Events.GetBackupFailed(e, this.configFilePath);
                throw;
            }
        }

        private async Task BackupModuleSet(ModuleSet updated)
        {
            try
            {
                string json = this.moduleSetSerde.Serialize(updated);
                // Serialize writes to the backup from GetModuleSetAsync and the ModuleSetChanged
                // event notification
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

        private async Task ApplyDiff(Diff diff)
        {
            try
            {
                using (await this.sync.LockAsync())
                {
                    string json = await DiskFile.ReadAllAsync(this.configFilePath);
                    ModuleSet backup = this.moduleSetSerde.Deserialize(json);
                    backup = backup.ApplyDiff(diff);
                    await DiskFile.WriteAllAsync(this.configFilePath, this.moduleSetSerde.Serialize(backup));
                }
            }
            catch (FileNotFoundException e)
            {
                Events.BackupFileNotFound(e, this.configFilePath, diff);
                this.OnModuleSetFailed(e);
                // swallow the exception. we don't want to apply a diff before we have had a chance
                // to backup the config at least once
            }
            catch (Exception e)
            {
                Events.ApplyDiffFailed(e, this.configFilePath, diff);
                this.OnModuleSetFailed(e);
                // swallow the exception. failure to apply the diff shouldn't be fatal. let the agent
                // decide how to handle the module set failed event.
            }
        }

        private async Task OnModuleSetChanged(Diff diff)
        {
            await this.ApplyDiff(diff);
            this.ModuleSetChanged?.Invoke(this, diff);
        }

        protected virtual void OnModuleSetFailed(Exception ex)
        {
            this.ModuleSetFailed?.Invoke(this, ex);
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

            public static void ApplyDiffFailed(Exception exception, string filename, Diff diff)
            {
                Log.LogWarning((int)EventIds.ApplyDiffFailed, exception, $"FileBackupConfigSource failed applying {diff} to {filename}");
            }

            public static void BackupFileNotFound(Exception exception, string filename, Diff diff)
            {
                Log.LogWarning((int)EventIds.BackupFileNotFound, exception, $"FileBackupConfigSource failed applying {diff}. Backup file {filename} not created");
            }

            enum EventIds
            {
                Created = IdStart,
                SetBackupFailed,
                GetBackupFailed,
                RestoringFromBackup,
                ApplyDiffFailed,
                BackupFileNotFound
            }
        }
    }
}
