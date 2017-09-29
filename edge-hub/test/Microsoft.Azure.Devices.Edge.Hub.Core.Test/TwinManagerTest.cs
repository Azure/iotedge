namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
	using System;
	using System.Threading.Tasks;
	using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
	using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
	using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
	using Microsoft.Azure.Devices.Edge.Storage;
	using Microsoft.Azure.Devices.Edge.Util;
	using Microsoft.Azure.Devices.Edge.Util.Test.Common;
	using Microsoft.Azure.Devices.Shared;
	using Moq;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Xunit;

	[Unit]
	public class TwinManagerTest
	{
		Option<IEntityStore<string, TwinInfo>> twinStore;
		IMessageConverter<TwinCollection> twinCollectionMessageConverter;
		IMessageConverter<Twin> twinMessageConverter;

		public TwinManagerTest()
		{
			this.twinStore = Option.Some(new StoreProvider(new InMemoryDbStoreProvider()).GetEntityStore<string, TwinInfo>("default"));
			this.twinCollectionMessageConverter = new TwinCollectionMessageConverter();
			this.twinMessageConverter = new TwinMessageConverter();
		}

		[Fact]
		public void TwinManagerConstructorVerifiesArguments()
		{
			IConnectionManager connectionManager = Mock.Of<IConnectionManager>();
			Assert.Throws<ArgumentNullException>(() => new TwinManager(null, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore));
			Assert.Throws<ArgumentNullException>(() => new TwinManager(connectionManager, null, this.twinMessageConverter, this.twinStore));
			Assert.Throws<ArgumentNullException>(() => new TwinManager(connectionManager, this.twinCollectionMessageConverter, null, this.twinStore));
		}

		[Fact]
		public void TwinManagerConstructorWithValidArgumentsSucceeds()
		{
			IConnectionManager connectionManager = Mock.Of<IConnectionManager>();
			Assert.NotNull(new TwinManager(connectionManager, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore));
			Assert.NotNull(new TwinManager(connectionManager, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>()));
		}

		[Fact]
		public async void GetTwinWhenCloudOnlineTwinNotStoredSuccess()
		{
			// Arrange
			Twin twin = new Twin();
			IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

			Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
			mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
			Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

			string deviceId = "device";

			bool storeHit = false;
			bool storeMiss = false;

			// Act
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

			// Assert
			Assert.Equal(storeHit, false);
			Assert.Equal(storeMiss, true);

			// Act
			IMessage received = await twinManager.GetTwinAsync(deviceId);

			// Assert
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));
			Assert.Equal(storeHit, true);
		}

		[Fact]
		public async void GetTwinWhenCloudOfflineSuccess()
		{
			// Arrange
			Twin twin = new Twin();
			IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

			Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
			mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
			Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

			string deviceId = "device";
			await twinManager.GetTwinAsync(deviceId);

			bool getTwinCalled = false;
			bool storeHit = false;
			mockProxy.Setup(t => t.GetTwinAsync()).Callback(() => getTwinCalled = true).Throws(new Exception("Offline"));

			// Act
			IMessage received = await twinManager.GetTwinAsync(deviceId);

			// Assert
			Assert.Equal(getTwinCalled, true);
			Assert.Equal(received.Body, twinMessage.Body);
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));
			Assert.Equal(storeHit, true);
		}

		[Fact]
		public async void GetTwinPassthroughWhenTwinNotStoredSuccess()
		{
			// Arrange
			Twin twin = new Twin();
			IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

			Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
			mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
			Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>());

			string deviceId = "device";

			// Act
			IMessage received = await twinManager.GetTwinAsync(deviceId);

			// Assert
			Assert.Equal(received.Body, twinMessage.Body);
			Assert.Equal(twinManager.TwinStore, Option.None<IEntityStore<string, TwinInfo>>());
		}

		[Fact]
		public async void UpdateDesiredPropertiesWhenTwinStoredSuccess()
		{
			// Arrange
			Twin twin = new Twin();
			twin.Properties.Desired = new TwinCollection()
			{
				["name"] = "original",
				["$version"] = 32
			};
			IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

			Mock<ICloudProxy> mockCloudProxy = new Mock<ICloudProxy>();
			mockCloudProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
			Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

			Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
			Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);
			connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

			string deviceId = "device";
			IMessage received = await twinManager.GetTwinAsync(deviceId);

			TwinCollection collection = new TwinCollection()
			{
				["name"] = "value",
				["$version"] = 33
			};
			IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

			// Act
			await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage);
			TwinInfo patched = null;
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { patched = t; return Task.CompletedTask; }, () => Task.FromResult<TwinInfo>(null));

			// Assert
			Assert.Equal(patched.Twin.Properties.Desired.ToJson(), collection.ToJson());
		}

		[Fact]
		public async void UpdateDesiredPropertiesWhenTwinNotStoredSuccess()
		{
			// Arrange
			Twin twin = new Twin();
			IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

			bool getTwinCalled = false;
			Mock<ICloudProxy> mockCloudProxy = new Mock<ICloudProxy>();
			mockCloudProxy.Setup(t => t.GetTwinAsync()).Callback(() => getTwinCalled = true).Returns(Task.FromResult(twinMessage));
			Option<ICloudProxy> cloudProxy = Option.Some(mockCloudProxy.Object);

			Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
			Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);
			connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

			string deviceId = "device";
			TwinCollection collection = new TwinCollection()
			{
				["name"] = "value",
				["$version"] = 33
			};
			IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);
			bool storeHit = false;

			// Act
			await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage);
			TwinInfo updated = null;
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; updated = t; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));

			// Assert
			Assert.Equal(getTwinCalled, true);
			Assert.Equal(storeHit, true);
			Assert.Equal(updated.Twin.Properties.Desired, collection);
		}

		[Fact]
		public async void UpdateDesiredPropertiesPassthroughSuccess()
		{
			// Arrange
			TwinCollection received = new TwinCollection();

			Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
			Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);
			mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
				.Callback<IMessage>((m) =>
				{ received = this.twinCollectionMessageConverter.FromMessage(m); })
				.Returns(Task.CompletedTask);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

			string deviceId = "device";
			TwinCollection collection = new TwinCollection()
			{
				["name"] = "value",
				["$version"] = 33
			};
			IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>());

			// Act
			await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage);

			// Assert
			Assert.Equal(received, collection);
			Assert.Equal(twinManager.TwinStore, Option.None<IEntityStore<string, TwinInfo>>());
		}

		[Fact]
		public void ApplyDiffSuccess()
		{
			// Arrange
			TwinCollection baseline = new TwinCollection()
			{
				["name"] = new TwinCollection()
				{
					["level0"] = "nochange",
					["level1"] = "value1",
					["level2"] = new TwinCollection()
					{
						["level3"] = "value3"
					},
					["level6"] = null,
				},
				["overwrite"] = new TwinCollection()
				{
					["level1"] = "value1"
				},
				["create"] = "yes",
				["$version"] = 33
			};

			TwinCollection patch = new TwinCollection()
			{
				["name"] = new TwinCollection()
				{
					["level0"] = "nochange", // unchanged
					["level1"] = null, // existing in base. remove property, only if treatNullAsDelete = true
					["level2"] = new TwinCollection()
					{
						["level3"] = "newvalue3" // existing in base, update property
					},
					["level4"] = "value4", // non existant in base, add new property
					["level5"] = null // ignore, unless treatNullAsDelete = false
				},
				["overwrite"] = "yes", // overwrite object with value
				["create"] = new TwinCollection() // overwrite value with object
				{
					["level1"] = "value1",
				},
				["$version"] = 34
			};

			TwinCollection mergedExcludeNull = new TwinCollection()
			{
				["name"] = new TwinCollection()
				{
					["level0"] = "nochange",
					["level2"] = new TwinCollection()
					{
						["level3"] = "newvalue3"
					},
					["level4"] = "value4",
					["level6"] = null
				},
				["overwrite"] = "yes",
				["create"] = new TwinCollection()
				{
					["level1"] = "value1",
				},
				["$version"] = 34
			};

			TwinCollection mergedIncludeNull = new TwinCollection()
			{
				["name"] = new TwinCollection()
				{
					["level0"] = "nochange",
					["level1"] = null,
					["level2"] = new TwinCollection()
					{
						["level3"] = "newvalue3"
					},
					["level4"] = "value4",
					["level5"] = null,
					["level6"] = null
				},
				["overwrite"] = "yes",
				["create"] = new TwinCollection()
				{
					["level1"] = "value1",
				},
				["$version"] = 34
			};

			TwinCollection emptyBaseline = new TwinCollection();

			TwinCollection emptyPatch = new TwinCollection();

			// Act
			TwinCollection resultCollection = TwinManager.MergeTwinCollections(baseline, patch, true);

			// Assert
			Assert.True(JToken.DeepEquals(JsonConvert.DeserializeObject<JToken>(resultCollection.ToJson()), JsonConvert.DeserializeObject<JToken>(mergedExcludeNull.ToJson())));

			// Act
			resultCollection = TwinManager.MergeTwinCollections(baseline, patch, false);

			// Assert
			Assert.True(JToken.DeepEquals(JsonConvert.DeserializeObject<JToken>(resultCollection.ToJson()), JsonConvert.DeserializeObject<JToken>(mergedIncludeNull.ToJson())));

			// Act
			resultCollection = TwinManager.MergeTwinCollections(emptyBaseline, emptyPatch, true);

			// Assert
			Assert.True(JToken.DeepEquals(JsonConvert.DeserializeObject<JToken>(resultCollection.ToJson()), JsonConvert.DeserializeObject<JToken>(emptyBaseline.ToJson())));

			// Act
			resultCollection = TwinManager.MergeTwinCollections(baseline, emptyPatch, true);

			// Assert
			Assert.True(JToken.DeepEquals(JsonConvert.DeserializeObject<JToken>(resultCollection.ToJson()), JsonConvert.DeserializeObject<JToken>(baseline.ToJson())));

			// Act
			resultCollection = TwinManager.MergeTwinCollections(emptyBaseline, patch, true);

			// Assert
			Assert.True(JToken.DeepEquals(JsonConvert.DeserializeObject<JToken>(resultCollection.ToJson()), JsonConvert.DeserializeObject<JToken>(patch.ToJson())));
		}

		[Fact]
		public async void UpdateReportedPropertiesPassthroughSuccess()
		{
			// Arrange
			IMessage receivedMessage = null;
			Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
			mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
				.Callback<IMessage>((m) => receivedMessage = m)
				.Returns(Task.CompletedTask);
			Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

			string deviceId = "device";
			TwinCollection collection = new TwinCollection()
			{
				["name"] = "value",
				["$version"] = 33
			};
			IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, Option.None<IEntityStore<string, TwinInfo>>());

			// Act
			await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

			// Assert
			Assert.Equal(collectionMessage, receivedMessage);
			Assert.Equal(twinManager.TwinStore, Option.None<IEntityStore<string, TwinInfo>>());
		}

		[Fact]
		public async void UpdateReportedPropertiesWhenCloudOnlineTwinNotStoredSuccess()
		{
			// Arrange
			Twin twin = new Twin();
			twin.Properties.Reported = new TwinCollection()
			{
				["name"] = "oldvalue",
				["$version"] = 32
			};
			IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

			IMessage receivedMessage = null;
			Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
			mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
				.Callback<IMessage>((m) => receivedMessage = m)
				.Returns(Task.CompletedTask);
			mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
			Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

			string deviceId = "device";
			TwinCollection collection = new TwinCollection()
			{
				["name"] = "value",
				["$version"] = 33
			};
			IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

			bool storeMiss = false;
			bool storeHit = false;

			// Act - find the twin
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

			// Assert - verify that the twin is not in the store
			Assert.Equal(storeHit, false);
			Assert.Equal(storeMiss, true);

			// Act
			await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

			// Assert - verify that the message was sent to the cloud proxy
			Assert.Equal(receivedMessage, collectionMessage);

			// Assert - verify that the twin was fetched
			storeMiss = false;
			storeHit = false;
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });
			Assert.Equal(storeHit, true);

			// Assert - verify that the local twin's reported properties updated.
			// verify that the local patch of reported properties was not updated.
			TwinInfo retrieved = null;
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { retrieved = t; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));
			Assert.Equal(storeMiss, false);
			Assert.True(JToken.DeepEquals(
				JsonConvert.DeserializeObject<JToken>(retrieved.Twin.Properties.Reported.ToJson()),
				JsonConvert.DeserializeObject<JToken>(collection.ToJson())));

			Assert.True(JToken.DeepEquals(
				JsonConvert.DeserializeObject<JToken>(retrieved.ReportedPropertiesPatch.ToJson()),
				JsonConvert.DeserializeObject<JToken>((new TwinCollection()).ToJson())));
		}

		[Fact]
		public async void UpdateReportedPropertiesWhenCloudOfflineTwinNotStoredSuccess()
		{
			// Arrange
			Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
			mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
				.Throws(new Exception("Not interested"));
			mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));
			Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

			string deviceId = "device";
			TwinCollection collection = new TwinCollection()
			{
				["name"] = "value",
				["$version"] = 33
			};
			IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

			bool storeMiss = false;
			bool storeHit = false;

			// Act
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

			// Assert
			Assert.Equal(storeHit, false);
			Assert.Equal(storeMiss, true);

			// Act
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage));
		}

		[Fact]
		public async void UpdateReportedPropertiesWhenCloudOfflineTwinStoredSuccess()
		{
			// Arrange
			Twin twin = new Twin();
			twin.Properties.Reported = new TwinCollection()
			{
				["name"] = "oldvalue",
				["$version"] = 32
			};
			IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

			Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
			mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
			Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

			string deviceId = "device";
			TwinCollection collection = new TwinCollection()
			{
				["name"] = "value",
				["$version"] = 33
			};
			IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

			bool storeMiss = false;
			bool storeHit = false;

			// Act
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

			// Assert
			Assert.Equal(storeHit, false);
			Assert.Equal(storeMiss, true);

			// Act
			await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

			// Assert - verify that the twin was fetched
			storeMiss = false;
			storeHit = false;
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });
			Assert.Equal(storeHit, true);

			// Arrange - make the cloud offline
			mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
				.Throws(new Exception("Not interested"));
			mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));

			TwinCollection patch = new TwinCollection()
			{
				["name"] = null,
				["newname"] = "value",
				["$version"] = 33
			};
			TwinCollection merged = new TwinCollection()
			{
				["newname"] = "value",
				["$version"] = 33
			};
			IMessage patchMessage = this.twinCollectionMessageConverter.ToMessage(patch);

			// Act
			await twinManager.UpdateReportedPropertiesAsync(deviceId, patchMessage);

			// Assert - verify that the twin's reported properties was updated and that the patch was stored
			TwinInfo retrieved = null;
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { retrieved = t; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));
			Assert.True(JToken.DeepEquals(
				JsonConvert.DeserializeObject<JToken>(retrieved.Twin.Properties.Reported.ToJson()),
				JsonConvert.DeserializeObject<JToken>(merged.ToJson())));
			Assert.True(JToken.DeepEquals(
				JsonConvert.DeserializeObject<JToken>(retrieved.ReportedPropertiesPatch.ToJson()),
				JsonConvert.DeserializeObject<JToken>(patch.ToJson())));
		}

		[Fact]
		public async void AllOperationsWhenCloudOfflineTwinNotStoredThrows()
		{
			// Arrange
			Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
			mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
				.Throws(new Exception("Not interested"));
			mockProxy.Setup(t => t.GetTwinAsync()).Throws(new Exception("Not interested"));
			Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

			TwinCollection collection = new TwinCollection()
			{
				["name"] = "value",
				["$version"] = 33
			};
			IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

			string deviceId = "device";

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

			// Act and Assert
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage));
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await twinManager.GetTwinAsync(deviceId));
		}

		[Fact]
		public async void UpdateDesiredPropertiesWhenDeviceProxyOfflineThrows()
		{
			// Arrange
			Mock<IDeviceProxy> mockDeviceProxy = new Mock<IDeviceProxy>();
			mockDeviceProxy.Setup(t => t.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
				.Throws(new Exception("Not interested"));
			Option<IDeviceProxy> deviceProxy = Option.Some(mockDeviceProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetDeviceConnection(It.IsAny<string>())).Returns(deviceProxy);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

			string deviceId = "device";

			TwinCollection collection = new TwinCollection()
			{
				["name"] = "value",
				["$version"] = 33
			};
			IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

			// Act and Assert
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await twinManager.UpdateDesiredPropertiesAsync(deviceId, collectionMessage));
		}

		[Fact]
		public async void GetTwinDoesNotOverwriteSavedReportedPropertiesSuccess()
		{
			// Arrange
			Twin twin = new Twin();
			IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

			Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
			mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
			Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, this.twinStore);

			string deviceId = "device";

			bool storeHit = false;
			bool storeMiss = false;

			// Act
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => { storeMiss = true; return Task.FromResult<TwinInfo>(null); });

			// Assert - verify that the twin is not in the store
			Assert.Equal(storeHit, false);
			Assert.Equal(storeMiss, true);

			// Act
			await twinManager.GetTwinAsync(deviceId);

			// Assert - verify that the twin is in the store
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { storeHit = true; return Task.FromResult(t); }, () => Task.FromResult<TwinInfo>(null));
			Assert.Equal(storeHit, true);

			// Arrange - update reported properties when offline
			mockProxy.Setup(t => t.UpdateReportedPropertiesAsync(It.IsAny<IMessage>()))
				.Throws(new Exception("Not interested"));

			TwinCollection collection = new TwinCollection()
			{
				["name"] = "newvalue",
				["$version"] = 33
			};
			IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

			// Act - update reported properties in offline mode
			await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage);

			// Act - find the twin and reported property patch
			TwinInfo cached = null;
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t; return Task.CompletedTask; }, () => Task.CompletedTask);

			// Assert - verify that the patch and the twin were updated
			Assert.True(JToken.DeepEquals(
				JsonConvert.DeserializeObject<JToken>(cached.Twin.Properties.Reported.ToJson()),
				JsonConvert.DeserializeObject<JToken>(collection.ToJson())));
			Assert.True(JToken.DeepEquals(
				JsonConvert.DeserializeObject<JToken>(cached.ReportedPropertiesPatch.ToJson()),
				JsonConvert.DeserializeObject<JToken>(collection.ToJson())));

			// Act - get twin so that the local twin gets updated
			await twinManager.GetTwinAsync(deviceId);

			// Assert - verify that the twin was updated but patch was not
			await twinManager.ExecuteOnTwinStoreResultAsync(deviceId, t => { cached = t; return Task.CompletedTask; }, () => Task.CompletedTask);

			Assert.True(JToken.DeepEquals(
				JsonConvert.DeserializeObject<JToken>(cached.Twin.ToJson()),
				JsonConvert.DeserializeObject<JToken>(twin.ToJson())));
			Assert.True(JToken.DeepEquals(
				JsonConvert.DeserializeObject<JToken>(cached.ReportedPropertiesPatch.ToJson()),
				JsonConvert.DeserializeObject<JToken>(collection.ToJson())));
		}

		[Fact]
		public async void GetTwinWhenStorePutFailsReturnsLastKnownSuccess()
		{
			// Arrange
			Twin twin = new Twin();
			IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

			Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
			mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
			Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

			Mock<IEntityStore<string, TwinInfo>> mockTwinStore = new Mock<IEntityStore<string, TwinInfo>>();
			mockTwinStore.Setup(t => t.PutOrUpdate(It.IsAny<string>(), It.IsAny<TwinInfo>(), It.IsAny<Func<TwinInfo, TwinInfo>>()))
				.Returns(Task.CompletedTask);

			Option<IEntityStore<string, TwinInfo>> twinStore = Option.Some<IEntityStore<string, TwinInfo>>(mockTwinStore.Object);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, twinStore);

			string deviceId = "device";

			// Act - cache the twin in the store
			IMessage received = await twinManager.GetTwinAsync(deviceId);

			// Assert - verify we got the cloud copy back
			Assert.Equal(received.Body, twinMessage.Body);

			// Arrange - setup failure of twin store
			bool getCalled = false;
			mockTwinStore.Setup(t => t.PutOrUpdate(It.IsAny<string>(), It.IsAny<TwinInfo>(), It.IsAny<Func<TwinInfo, TwinInfo>>()))
				.Throws(new Exception("Out of space"));
			mockTwinStore.Setup(t => t.Get(It.IsAny<string>()))
				.Callback(() => getCalled = true)
				.Returns(Task.FromResult(Option.Some<TwinInfo>(new TwinInfo(twin, null))));

			// Change what the cloud returns
			Twin newTwin = new Twin();
			IMessage newTwinMessage = this.twinMessageConverter.ToMessage(twin);
			mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(newTwinMessage));

			// Act - cache the twin in the store
			received = await twinManager.GetTwinAsync(deviceId);

			// Assert - verify we got the old value of the twin
			Assert.Equal(getCalled, true);
			Assert.Equal(received.Body, twinMessage.Body);
		}

		[Fact]
		public async void UpdateReportedPropertiesWhenStoreThrowsFailure()
		{
			// Arrange
			Twin twin = new Twin();
			IMessage twinMessage = this.twinMessageConverter.ToMessage(twin);

			Mock<ICloudProxy> mockProxy = new Mock<ICloudProxy>();
			mockProxy.Setup(t => t.GetTwinAsync()).Returns(Task.FromResult(twinMessage));
			Option<ICloudProxy> cloudProxy = Option.Some<ICloudProxy>(mockProxy.Object);

			Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
			connectionManager.Setup(t => t.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxy);

			Mock<IEntityStore<string, TwinInfo>> mockTwinStore = new Mock<IEntityStore<string, TwinInfo>>();
			mockTwinStore.Setup(t => t.PutOrUpdate(It.IsAny<string>(), It.IsAny<TwinInfo>(), It.IsAny<Func<TwinInfo, TwinInfo>>()))
				.Returns(Task.CompletedTask);

			Option<IEntityStore<string, TwinInfo>> twinStore = Option.Some<IEntityStore<string, TwinInfo>>(mockTwinStore.Object);

			TwinManager twinManager = new TwinManager(connectionManager.Object, this.twinCollectionMessageConverter, this.twinMessageConverter, twinStore);

			mockTwinStore.Setup(t => t.PutOrUpdate(It.IsAny<string>(), It.IsAny<TwinInfo>(), It.IsAny<Func<TwinInfo, TwinInfo>>()))
				.Throws(new Exception("Out of space"));
			mockTwinStore.Setup(t => t.Get(It.IsAny<string>())).ReturnsAsync(Option.None<TwinInfo>());

			string deviceId = "device";
			TwinCollection collection = new TwinCollection()
			{
				["name"] = "newvalue",
				["$version"] = 33
			};
			IMessage collectionMessage = this.twinCollectionMessageConverter.ToMessage(collection);

			await Assert.ThrowsAsync<InvalidOperationException>(async () => await twinManager.UpdateReportedPropertiesAsync(deviceId, collectionMessage));
		}
	}
}
