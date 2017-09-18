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
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	public class TwinManager : ITwinManager
	{
		readonly IMessageConverter<TwinCollection> twinCollectionConverter;
		readonly IMessageConverter<Twin> twinConverter;
		readonly IConnectionManager connectionManager;
		internal Option<IEntityStore<string, Twin>> twinStore { get; }

		public TwinManager(IConnectionManager connectionManager, IMessageConverter<TwinCollection> twinCollectionConverter, IMessageConverter<Twin> twinConverter, Option<IEntityStore<string, Twin>> twinStore)
		{
			this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
			this.twinCollectionConverter = Preconditions.CheckNotNull(twinCollectionConverter, nameof(twinCollectionConverter));
			this.twinConverter = Preconditions.CheckNotNull(twinConverter, nameof(twinConverter));
			this.twinStore = Preconditions.CheckNotNull(twinStore, nameof(twinStore));
		}

		public static ITwinManager CreateTwinManager(IConnectionManager connectionManager, IMessageConverterProvider messageConverterProvider, Option<IStoreProvider> storeProvider)
		{
			Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
			Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
			Preconditions.CheckNotNull(storeProvider, nameof(storeProvider));
			return new TwinManager(connectionManager, messageConverterProvider.Get<TwinCollection>(), messageConverterProvider.Get<Twin>(),
				storeProvider.Match(
					s => Option.Some(s.GetEntityStore<string, Twin>(Constants.TwinStorePartitionKey)),
					() => Option.None<IEntityStore<string, Twin>>()));
		}

		public async Task<IMessage> GetTwinAsync(string id)
		{
			return await this.twinStore.Match(
				async (store) =>
				{
					Twin twin = await this.GetTwinWithStoreSupportAsync(id);
					return this.twinConverter.ToMessage(twin);
				},
				async () =>
				{
					// pass through to cloud proxy
					Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(id);
					return await cloudProxy.Match(async (cp) => await cp.GetTwinAsync(), () => throw new Exception($"Cloud proxy unavailable for device {id}"));
				});
		}

		async Task<Twin> GetTwinWithStoreSupportAsync(string id)
		{
			try
			{
				Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(id);
				return await cloudProxy.Match(
						async (cp) =>
						{
							return await this.GetTwinWhenCloudOnlineAsync(id, cp);
						},
						async () => await this.GetTwinWhenCloudOfflineAsync(id, new Exception($"Error accessing cloud proxy for device {id}"))
				);
			}
			catch (Exception e)
			{
				return await this.GetTwinWhenCloudOfflineAsync(id, e);
			}
		}

		internal async Task<Twin> GetTwinFromStoreAsync(string id, Func<Twin, Task<Twin>> twinStoreHit, Func<Task<Twin>> twinStoreMiss)
		{
			Option<Twin> cached = await this.twinStore.Match(async (s) => await s.Get(id), () => throw new Exception("Missing twin store"));
			return await cached.Match(twinStoreHit, twinStoreMiss);
		}

		public async Task UpdateDesiredPropertiesAsync(string id, IMessage desiredProperties)
		{
			await this.twinStore.Match(
				async (s) => await this.UpdateDesiredPropertiesWithStoreSupportAsync(id, desiredProperties),
				async () =>
				{
					// pass through to device proxy
					Option<IDeviceProxy> deviceProxy = this.connectionManager.GetDeviceConnection(id);
					await deviceProxy.Match(dp => dp.OnDesiredPropertyUpdates(desiredProperties), () => throw new Exception($"Device proxy unavailable for device {id}"));
				});
		}

		async Task UpdateDesiredPropertiesWithStoreSupportAsync(string id, IMessage desiredProperties)
		{
			try
			{
				TwinCollection desired = this.twinCollectionConverter.FromMessage(desiredProperties);
				await this.GetTwinFromStoreAsync(
					id,
					async (t) => await this.UpdateDesiredPropertiesWhenTwinStoreHasTwinAsync(id, t, desired),
					async () => await this.UpdateDesiredPropertiesWhenTwinStoreNeedsTwinAsync(id, desired));
				Option<IDeviceProxy> deviceProxy = this.connectionManager.GetDeviceConnection(id);
				await deviceProxy.Match(dp => dp.OnDesiredPropertyUpdates(desiredProperties), () => throw new Exception($"Device proxy unavailable for device {id}"));
			}
			catch (Exception e)
			{
				throw new Exception($"Error updating desired properties for device {id}", e);
			}
		}

		Twin UpdateTwinStoreDesiredProperties(Twin old, TwinCollection diff)
		{
			TwinCollection merged = MergeTwinCollections(old.Properties.Desired, diff, true);
			old.Properties.Desired = merged;
			return old;
		}

		async Task<Twin> UpdateDesiredPropertiesWhenTwinStoreHasTwinAsync(string id, Twin twin, TwinCollection desired)
		{
			Twin updated = twin;
			await this.twinStore.Match(async (s) => await s.PutOrUpdate(id, twin,
				u => updated = this.UpdateTwinStoreDesiredProperties(u, desired)), () => throw new Exception("Missing twin store"));
			return await Task.FromResult(updated);
		}

		async Task<Twin> UpdateDesiredPropertiesWhenTwinStoreNeedsTwinAsync(string id, TwinCollection desired)
		{
			Twin twin = await this.GetTwinWithStoreSupportAsync(id);
			return await this.UpdateDesiredPropertiesWhenTwinStoreHasTwinAsync(id, twin, desired);
		}

		async Task<Twin> GetTwinWhenCloudOnlineAsync(string id, ICloudProxy cp)
		{
			IMessage twinMessage = await cp.GetTwinAsync();
			Twin twin = twinConverter.FromMessage(twinMessage);
			await this.twinStore.Match(async (s) => await s.PutOrUpdate(id, twin, t => twin), () => throw new Exception("Missing twin store"));
			return twin;
		}

		async Task<Twin> GetTwinWhenCloudOfflineAsync(string id, Exception e)
		{
			Twin twin = await this.GetTwinFromStoreAsync(id, t => Task.FromResult(t), () => throw new Exception($"Error getting twin for device {id}", e));
			IMessage twinMessage = twinConverter.ToMessage(twin);
			return twin;
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
	}
}
