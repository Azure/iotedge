// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
	using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
	using Microsoft.Azure.Devices.Edge.Storage;
	using Microsoft.Azure.Devices.Edge.Util;
	using Microsoft.Azure.Devices.Shared;
	using Microsoft.Extensions.Logging;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	public class TwinManager : ITwinManager
	{
		readonly IMessageConverter<TwinCollection> twinCollectionConverter;
		readonly IMessageConverter<Twin> twinConverter;
		readonly IConnectionManager connectionManager;
		internal Option<IEntityStore<string, TwinInfo>> TwinStore { get; }

		public TwinManager(IConnectionManager connectionManager, IMessageConverter<TwinCollection> twinCollectionConverter, IMessageConverter<Twin> twinConverter, Option<IEntityStore<string, TwinInfo>> twinStore)
		{
			this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
			this.twinCollectionConverter = Preconditions.CheckNotNull(twinCollectionConverter, nameof(twinCollectionConverter));
			this.twinConverter = Preconditions.CheckNotNull(twinConverter, nameof(twinConverter));
			this.TwinStore = Preconditions.CheckNotNull(twinStore, nameof(twinStore));
		}

		public static ITwinManager CreateTwinManager(IConnectionManager connectionManager, IMessageConverterProvider messageConverterProvider, Option<IStoreProvider> storeProvider)
		{
			Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
			Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
			Preconditions.CheckNotNull(storeProvider, nameof(storeProvider));
			return new TwinManager(connectionManager, messageConverterProvider.Get<TwinCollection>(), messageConverterProvider.Get<Twin>(),
				storeProvider.Match(
					s => Option.Some(s.GetEntityStore<string, TwinInfo>(Constants.TwinStorePartitionKey)),
					() => Option.None<IEntityStore<string, TwinInfo>>()));
		}

		public async Task<IMessage> GetTwinAsync(string id)
		{
			return await this.TwinStore.Match(
				async (store) =>
				{
					TwinInfo twinInfo = await this.GetTwinInfoWithStoreSupportAsync(id);
					return this.twinConverter.ToMessage(twinInfo.Twin);
				},
				async () =>
				{
					// pass through to cloud proxy
					Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(id);
					return await cloudProxy.Match(async (cp) => await cp.GetTwinAsync(), () => throw new InvalidOperationException($"Cloud proxy unavailable for device {id}"));
				});
		}

		async Task<TwinInfo> GetTwinInfoWithStoreSupportAsync(string id)
		{
			try
			{
				Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(id);
				return await cloudProxy.Match(
						async (cp) =>
						{
							return await this.GetTwinInfoWhenCloudOnlineAsync(id, cp);
						},
						async () => await this.GetTwinInfoWhenCloudOfflineAsync(id, new InvalidOperationException($"Error accessing cloud proxy for device {id}"))
				);
			}
			catch (Exception e)
			{
				return await this.GetTwinInfoWhenCloudOfflineAsync(id, e);
			}
		}

		internal async Task ExecuteOnTwinStoreResultAsync(string id, Func<TwinInfo, Task> twinStoreHit, Func<Task> twinStoreMiss)
		{
			Option<TwinInfo> cached = await this.TwinStore.Match(async (s) => await s.Get(id), () => throw new InvalidOperationException("Missing twin store"));
			await cached.Match(async (c) => await twinStoreHit(c), async () => await twinStoreMiss());
		}

		public async Task UpdateDesiredPropertiesAsync(string id, IMessage desiredProperties)
		{
			await this.TwinStore.Match(
				async (s) => await this.UpdateDesiredPropertiesWithStoreSupportAsync(id, desiredProperties),
				async () => await this.SendDesiredPropertiesToDeviceProxy(id, desiredProperties));
		}

		async Task SendDesiredPropertiesToDeviceProxy(string id, IMessage desired)
		{
			Option<IDeviceProxy> deviceProxy = this.connectionManager.GetDeviceConnection(id);
			await deviceProxy.Match(dp => dp.OnDesiredPropertyUpdates(desired), () => throw new InvalidOperationException($"Device proxy unavailable for device {id}"));
		}

		async Task UpdateDesiredPropertiesWithStoreSupportAsync(string id, IMessage desiredProperties)
		{
			try
			{
				TwinCollection desired = this.twinCollectionConverter.FromMessage(desiredProperties);
				await this.ExecuteOnTwinStoreResultAsync(
					id,
					async (t) => await this.UpdateDesiredPropertiesWhenTwinStoreHasTwinAsync(id, t, desired),
					async () => await this.UpdateDesiredPropertiesWhenTwinStoreNeedsTwinAsync(id, desired));
				await this.SendDesiredPropertiesToDeviceProxy(id, desiredProperties);
			}
			catch (Exception e)
			{
				throw new InvalidOperationException($"Error processing desired properties for device {id}", e);
			}
		}

		async Task UpdateDesiredPropertiesWhenTwinStoreHasTwinAsync(string id, TwinInfo twinInfo, TwinCollection desired)
		{
			await this.TwinStore.Match(
				async (s) => await s.Update(
					id,
					u =>
					{
						u.Twin.Properties.Desired = MergeTwinCollections(u.Twin.Properties.Desired, desired, true);
						return u;
					}),
				() => throw new InvalidOperationException("Missing twin store"));
		}

		async Task UpdateDesiredPropertiesWhenTwinStoreNeedsTwinAsync(string id, TwinCollection desired)
		{
			TwinInfo twinInfo = await this.GetTwinInfoWithStoreSupportAsync(id);
			await this.UpdateDesiredPropertiesWhenTwinStoreHasTwinAsync(id, twinInfo, desired);
		}

		async Task<TwinInfo> GetTwinInfoWhenCloudOnlineAsync(string id, ICloudProxy cp)
		{
			IMessage twinMessage = await cp.GetTwinAsync();
			Twin cloudTwin = twinConverter.FromMessage(twinMessage);

			TwinInfo updated = new TwinInfo(cloudTwin, null);
			await this.TwinStore.Match(
				async (s) => await s.PutOrUpdate(
					id,
					updated,
					t =>
					{
						updated = new TwinInfo(cloudTwin, t.ReportedPropertiesPatch);
						return updated;
					}),
				() => throw new InvalidOperationException("Missing twin store"));
			return updated;
		}

		async Task<TwinInfo> GetTwinInfoWhenCloudOfflineAsync(string id, Exception e)
		{
			TwinInfo twinInfo = null;
			await this.ExecuteOnTwinStoreResultAsync(
				id,
				t =>
				{
					twinInfo = t;
					return Task.CompletedTask;
				},
				() => throw new InvalidOperationException($"Error getting twin for device {id}"));
			return twinInfo;
		}

		async Task UpdateReportedPropertiesWhenTwinStoreHasTwinAsync(string id, TwinCollection reported)
		{
			await this.TwinStore.Match(
				async (s) => await s.Update(
					id,
					u =>
					{
						TwinCollection mergedProperty = MergeTwinCollections(u.Twin.Properties.Reported, reported, true /* treatNullAsDelete */);
						u.Twin.Properties.Reported = mergedProperty;
						return u;
					}),
				() => throw new InvalidOperationException("Missing twin store"));
		}

		async Task UpdateReportedPropertiesWhenTwinStoreNeedsTwinAsync(string id, TwinCollection reported)
		{
			TwinInfo twinInfo = await this.GetTwinInfoWithStoreSupportAsync(id);
			await this.UpdateReportedPropertiesWhenTwinStoreHasTwinAsync(id, reported);
		}

		public async Task UpdateReportedPropertiesAsync(string id, IMessage reportedProperties)
		{
			await this.TwinStore.Match(
				async (s) => await this.UpdateReportedPropertiesWithStoreSupportAsync(id, reportedProperties),
				async () => await this.SendReportedPropertiesToCloudProxy(id, reportedProperties));
		}

		async Task UpdateReportedPropertiesPatch(string id, TwinCollection reportedProperties)
		{
			await this.TwinStore.Match(
				async (s) => await s.Update(
					id,
					u =>
					{
						TwinCollection mergedPatch = null;
						if (u.ReportedPropertiesPatch == null)
						{
							mergedPatch = reportedProperties;
						}
						else
						{
							mergedPatch = MergeTwinCollections(u.ReportedPropertiesPatch, reportedProperties, false /* treatNullAsDelete */);
						}
						return new TwinInfo(u.Twin, mergedPatch);
					}),
				() => throw new InvalidOperationException("Missing twin store"));
		}

		async Task UpdateReportedPropertiesWithStoreSupportAsync(string id, IMessage reportedProperties)
		{
			TwinCollection reported = this.twinCollectionConverter.FromMessage(reportedProperties);

			try
			{
				// Update the local twin's reported properties
				await this.ExecuteOnTwinStoreResultAsync(
						id,
						async (t) => await this.UpdateReportedPropertiesWhenTwinStoreHasTwinAsync(id, reported),
						async () => await this.UpdateReportedPropertiesWhenTwinStoreNeedsTwinAsync(id, reported));
			}
			catch (Exception e)
			{
				throw new InvalidOperationException($"Error updating reported properties for device {id}", e);
			}

			try
			{
				// TODO handle the case where patches have accumulated. co-ordinate with the connection
				// callback to handle pending patches
				await this.SendReportedPropertiesToCloudProxy(id, reportedProperties);
			}
			catch (Exception e)
			{
				Events.UpdateReportedToCloudException(id, e);
				try
				{
					// Update the collective patch of reported properties
					await this.ExecuteOnTwinStoreResultAsync(
						id,
						async (t) => await this.UpdateReportedPropertiesPatch(id, reported),
						() => throw new InvalidOperationException($"Missing cached twin for device {id}"));
					return;
				}
				catch (Exception inner)
				{
					throw new InvalidOperationException($"Error updating reported properties for device {id}", inner);
				}
			}
		}

		async Task SendReportedPropertiesToCloudProxy(string id, IMessage reported)
		{
			Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(id);
			await cloudProxy.Match(async (cp) => await cp.UpdateReportedPropertiesAsync(reported), () => throw new InvalidOperationException($"Cloud proxy unavailable for device {id}"));
		}

		internal static TwinCollection MergeTwinCollections(TwinCollection baseline, TwinCollection patch, bool treatNullAsDelete)
		{
			Preconditions.CheckNotNull(baseline, nameof(baseline));
			Preconditions.CheckNotNull(patch, nameof(patch));
			JToken baselineToken = JToken.Parse(baseline.ToJson()).DeepClone();
			JToken patchToken = JToken.Parse(patch.ToJson()).DeepClone();
			return new TwinCollection(MergeTwinCollections(baselineToken, patchToken, treatNullAsDelete).ToJson());
		}

		static JToken MergeTwinCollections(JToken baseline, JToken patch, bool treatNullAsDelete)
		{
			// Reached the leaf JValue
			if ((patch is JValue) || (baseline.Type == JTokenType.Null) || (baseline is JValue))
			{
				return patch;
			}

			JsonSerializerSettings settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Include
			};

			Dictionary<string, JToken> patchDictionary = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(patch.ToJson(), settings);

			Dictionary<string, JToken> baselineDictionary = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(baseline.ToJson(), settings);

			Dictionary<string, JToken> result = baselineDictionary;
			foreach (KeyValuePair<string, JToken> patchPair in patchDictionary)
			{
				bool baselineContainsKey = baselineDictionary.ContainsKey(patchPair.Key);
				if (baselineContainsKey && (patchPair.Value.Type != JTokenType.Null))
				{
					JToken baselineValue = baselineDictionary[patchPair.Key];
					JToken nestedResult = MergeTwinCollections(baselineValue, patchPair.Value, treatNullAsDelete);
					result[patchPair.Key] = nestedResult;
				}
				else // decide whether to remove or add the patch key
				{
					if (treatNullAsDelete && (patchPair.Value.Type == JTokenType.Null))
					{
						result.Remove(patchPair.Key);
					}
					else
					{
						result[patchPair.Key] = patchPair.Value;
					}
				}
			}
			return JToken.FromObject(result);
		}

		static class Events
		{
			static readonly ILogger Log = Logger.Factory.CreateLogger<TwinManager>();
			const int IdStart = HubCoreEventIds.TwinManager;

			enum EventIds
			{
				UpdateReportedToCloudException = IdStart,
				StoreTwinFailed
			}

			public static void UpdateReportedToCloudException(string identity, Exception e)
			{
				Log.LogDebug((int)EventIds.UpdateReportedToCloudException, $"Updating reported properties for {identity} in cloud failed with error {e.GetType()} {e.Message}");
			}

			public static void StoreTwinFailed(string identity, Exception e, long v, long desired, long reported)
			{
				Log.LogDebug((int)EventIds.StoreTwinFailed, $"Storing twin for {identity} failed with error {e.GetType()} {e.Message}. Retrieving last stored twin with version {v}, desired version {desired} and reported version {reported}");
			}
		}
	}
}
