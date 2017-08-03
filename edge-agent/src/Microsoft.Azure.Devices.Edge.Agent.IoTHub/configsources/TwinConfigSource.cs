// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Azure.Devices.Edge.Agent.Core;
	using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
	using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
	using Microsoft.Azure.Devices.Edge.Util;
	using Microsoft.Azure.Devices.Shared;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.Logging;
	using Microsoft.Azure.Devices.Client;
	using Microsoft.Azure.Devices.Edge.Util.Concurrency;

	public class TwinConfigSource : BaseConfigSource
	{
		ISerde<ModuleSet> ModuleSetSerde { get; }

		ISerde<Diff> DiffSerde { get; }

		readonly IDeviceClient deviceClient;

		// Variables to track current module set and connection status
		internal ModuleSet CurrentModuleSet { get; set; }
		internal ConnectionStatus ConnectionStatus { get; set; }

		readonly Object twinRefreshLock;
		readonly AtomicBoolean getTwinInProgress;
		bool refreshTwin;

		TwinConfigSource(IDeviceClient deviceClient, ISerde<ModuleSet> moduleSetSerde, ISerde<Diff> diffSerde, IConfiguration configuration)
			: base(configuration)
		{
			this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
			this.ModuleSetSerde = Preconditions.CheckNotNull(moduleSetSerde, nameof(moduleSetSerde));
			this.DiffSerde = Preconditions.CheckNotNull(diffSerde, nameof(diffSerde));

			// Module state tracking
			this.CurrentModuleSet = ModuleSet.Empty;
			this.ConnectionStatus = ConnectionStatus.Disabled;
			this.twinRefreshLock = new Object();
			this.getTwinInProgress = new AtomicBoolean(false);
			this.refreshTwin = false;

			Events.Created();
		}

		Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
		{
			lock (this.twinRefreshLock)
			{
				// The assumption we are making is that if the device changes state from
				// offline to 'connected', the connection status change handler will be invoked
				// sufficiently in advance of the property update callback so that the
				// getTwinInProgress flag will be set by the time we reach here.
				if (this.getTwinInProgress.Get())
				{
					this.refreshTwin = true;
					Events.ModuleSetUpdateInProgress();
					return Task.CompletedTask;
				}
			}

			// In the off-chance that the property update callback is invoked before the connection status
			// changed handler, reject the update
			if (this.ConnectionStatus != ConnectionStatus.Connected)
			{
				Events.DesiredPropertyChangedWhileOffline(this.ConnectionStatus.ToString());
				Exception ex = new Exception($"Patch update received while status is {this.ConnectionStatus.ToString()}");
				this.OnFailed(ex);
				// Return successful completion, even though we didn't apply the patch, 
				// so that we don't throw an exception and kill the process
				return Task.CompletedTask;
			}

			try
			{
				if (this.CurrentModuleSet.Equals(ModuleSet.Empty))
				{
					// Should never reach here, since the module set should never
					// be empty if the connection status is 'connected'. Also our 
					// assumption is that the connection status handler will have
					// set the getTwinInProgress flag before the property update
					// callback ran resulting in us never reaching here.
					Exception ex = new InvalidOperationException("Current module set empty");
					Events.ModuleSetEmpty(ex);
					this.OnFailed(ex);
					return Task.FromException(ex);
				}
				Diff diff = this.DiffSerde.Deserialize(desiredProperties.ToJson());
				this.CurrentModuleSet = this.CurrentModuleSet.ApplyDiff(diff);
				this.OnModuleSetChanged(diff);
				return Task.CompletedTask;
			}
			catch (Exception ex) when (!ex.IsFatal())
			{
				Events.DesiredPropertiesFailed(ex);
				this.OnFailed(ex);
				return Task.FromException(ex);
			}
		}

		async void OnConnectionStatusChanged(ConnectionStatus status, ConnectionStatusChangeReason reason)
		{
			if ((this.ConnectionStatus != ConnectionStatus.Connected) && (status == ConnectionStatus.Connected))
			{
				try
				{
					this.getTwinInProgress.Set(true);

					// Refresh the twin
					bool refreshTwin = true;
					while (refreshTwin)
					{
						Twin twin = await deviceClient.GetTwinAsync();
						this.CurrentModuleSet = this.ModuleSetSerde.Deserialize(twin.Properties.Desired.ToJson());

						lock (this.twinRefreshLock)
						{
							// If a patch update was received while GetModuleSetAsync was in progress,
							// refresh the twin
							refreshTwin = this.refreshTwin;
							this.refreshTwin = false;
							if (!refreshTwin)
							{
								this.getTwinInProgress.Set(false);
							}
						}
					}
				}
				catch (Exception ex)
				{
					Events.GetTwinFailed(ex, status.ToString());
					throw;
				}
			}

			Events.ConnectionStatusChanged(this.ConnectionStatus.ToString(), status.ToString(), reason.ToString());
			this.ConnectionStatus = status;
		}

		public override void Dispose()
		{
			this.deviceClient.Dispose();
		}

		public override Task<ModuleSet> GetModuleSetAsync()
		{
			if (this.CurrentModuleSet.Equals(ModuleSet.Empty))
			{
				Exception ex = new InvalidOperationException("Current module set empty");
				this.OnFailed(ex);
				throw ex;
			}
			// Return the cached copy
			return Task.FromResult(this.CurrentModuleSet);
		}

		public override event EventHandler<Diff> ModuleSetChanged;

		public void OnModuleSetChanged(Diff diff)
		{
			this.ModuleSetChanged?.Invoke(this, diff);
		}

		public override event EventHandler<Exception> ModuleSetFailed;

		protected void OnFailed(Exception ex)
		{
			this.ModuleSetFailed?.Invoke(this, ex);
		}

		public static async Task<TwinConfigSource> Create(IDeviceClient deviceClient, ISerde<ModuleSet> moduleSetSerde, ISerde<Diff> diffSerde, IConfiguration configuration)
		{
			var configSource = new TwinConfigSource(deviceClient, moduleSetSerde, diffSerde, configuration);
			try
			{
				// SetConnectionStatusChangedHandler needs to be the first thing we do in order to get
				// a callback when the connection is established. If not, the ConnectionStatus variable will not
				// be updated from it's default Disabled state.
				configSource.deviceClient.SetConnectionStatusChangedHandler(configSource.OnConnectionStatusChanged);

				await configSource.deviceClient.SetDesiredPropertyUpdateCallback(configSource.OnDesiredPropertyChanged, null);
			}
			catch (Exception e)
			{
				Events.DeviceClientException(e);
			}
			return configSource;
		}

		static class Events
		{
			static readonly ILogger Log = Logger.Factory.CreateLogger<TwinConfigSource>();
			const int IdStart = AgentEventIds.TwinConfigSource;

			enum EventIds
			{
				Created = IdStart,
				DesiredPropertiesFailed,
				DeviceClientException,
				ModuleSetEmpty,
				ModuleSetUpdateInProgress,
				ConnectionStatusChanged,
				DesiredPropertyChangedWhileOffline,
				GetTwinFailed
			}

			public static void Created()
			{
				Log.LogDebug((int)EventIds.Created, "TwinConfigSource Created");
			}

			public static void DesiredPropertiesFailed(Exception exception)
			{
				Log.LogError((int)EventIds.DesiredPropertiesFailed, exception, "TwinConfigSource failed processing desired configuration");
			}

			public static void DeviceClientException(Exception exception)
			{
				Log.LogError((int)EventIds.DeviceClientException, exception, "TwinConfigSource got an exception from device client");
			}

			public static void ModuleSetEmpty(Exception ex)
			{
				Log.LogInformation((int)EventIds.ModuleSetEmpty, $"Cannot apply diff to module set. Reason {ex.GetType()} {ex.Message}");
			}

			public static void ConnectionStatusChanged(string old, string updated, string reason)
			{
				Log.LogInformation((int)EventIds.ConnectionStatusChanged, $"Connection status changed from {old} to {updated} with reason {reason}");
			}

			public static void DesiredPropertyChangedWhileOffline(string current)
			{
				Log.LogError((int)EventIds.DesiredPropertyChangedWhileOffline, $"Diff update received in disconnected connection state {current}");
			}

			public static void ModuleSetUpdateInProgress()
			{
				Log.LogDebug((int)EventIds.ModuleSetUpdateInProgress, "Diff callback signaled twin refresh");
			}

			public static void GetTwinFailed(Exception ex, string status)
			{
				Log.LogError((int)EventIds.GetTwinFailed, $"Get twin failed with {ex.GetType()} {ex.Message}. Connection status is {status}");
			}
		}
	}
}