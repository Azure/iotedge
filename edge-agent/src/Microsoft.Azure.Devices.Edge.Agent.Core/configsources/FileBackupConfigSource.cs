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

	public class FileBackupConfigSource : BaseConfigSource
	{
		readonly string configFilePath;
		readonly ISerde<ModuleSet> moduleSetSerde;
		readonly IConfigSource underlying;

		public FileBackupConfigSource(string path, ISerde<ModuleSet> moduleSetSerde, IConfigSource underlying, IConfiguration configuration) : base(configuration)
		{
			this.configFilePath = Preconditions.CheckNonWhiteSpace(path, nameof(path));
			this.moduleSetSerde = Preconditions.CheckNotNull(moduleSetSerde, nameof(moduleSetSerde));
			this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
			Preconditions.CheckNotNull(configuration, nameof(configuration));

			this.underlying.ModuleSetChanged += async (sender, updated) => await this.OnModuleSetChanged(updated);
			this.underlying.ModuleSetFailed += (sender, ex) => this.OnModuleSetFailed(ex);

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
			catch (Exception ex)
			{
				Events.RestoringFromBackup(ex, this.configFilePath);
				try
				{
					string json = await DiskFile.ReadAllAsync(this.configFilePath);
					return this.moduleSetSerde.Deserialize(json);
				}
				catch (Exception e)
				{
					Events.GetBackupFailed(e, this.configFilePath);
					this.OnModuleSetFailed(e);
					throw e;
				}
			}
		}

		public override event EventHandler<ModuleSetChangedArgs> ModuleSetChanged;

		public override event EventHandler<Exception> ModuleSetFailed;

		public override void Dispose()
		{
		}

		public async Task BackupModuleSet(ModuleSet updated)
		{
			try
			{
				string json = this.moduleSetSerde.Serialize(updated);
				await DiskFile.WriteAllAsync(this.configFilePath, json);
			}
			catch (Exception e)
			{
				Events.SetBackupFailed(e, this.configFilePath);
			}
		}

		private async Task OnModuleSetChanged(ModuleSetChangedArgs updated)
		{
			await this.BackupModuleSet(updated.ModuleSet);
			this.ModuleSetChanged?.Invoke(this, updated);
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
				Log.LogDebug((int)EventIds.Created, $"FileBackupConfigSource created with filename {filename}");
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