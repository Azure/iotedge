// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class UpdatableEncryptedStoreTest
    {
        [Fact]
        public async Task SmokeTest()
        {
            // Arrange
            IEncryptionProvider encryptionProvider = new TestEncryptionProvider();
            IEntityStore<string, string> entityStore = GetEntityStore<string, string>("smokeTest");
            IKeyValueStore<string, TestDevice> encryptedStore = new UpdatableEncryptedStore<string, TestDevice>(entityStore, encryptionProvider);
            string key = "device1";
            var device = new TestDevice(Guid.NewGuid().ToString(), new KeyAuth(Guid.NewGuid().ToString()));
            var deviceNew = new TestDevice(Guid.NewGuid().ToString(), new KeyAuth(Guid.NewGuid().ToString()));

            // Act / Assert
            bool contains = await encryptedStore.Contains("device1");
            Assert.False(contains);
            contains = await entityStore.Contains("device1");
            Assert.False(contains);

            // Add a value to the underlying store and make sure encrypted store returns it correctly
            await entityStore.Put(key, device.ToJson());

            contains = await encryptedStore.Contains("device1");
            Assert.True(contains);
            contains = await entityStore.Contains("device1");
            Assert.True(contains);

            Option<TestDevice> retrievedValue = await encryptedStore.Get("device1");
            Assert.True(retrievedValue.HasValue);
            Assert.Equal(device.GenId, retrievedValue.OrDefault().GenId);
            Assert.Equal(device.Auth.Key, retrievedValue.OrDefault().Auth.Key);

            Option<string> storedValue = await entityStore.Get("device1");
            Assert.True(storedValue.HasValue);
            TestDevice storedTestDevice = storedValue.OrDefault().FromJson<TestDevice>();

            Assert.Equal(device.GenId, storedTestDevice.GenId);
            Assert.Equal(device.Auth.Key, storedTestDevice.Auth.Key);

            // Add a value to the encrypted store and make sure underlying store sees the encrypted value
            await encryptedStore.Put(key, deviceNew);

            contains = await encryptedStore.Contains("device1");
            Assert.True(contains);

            retrievedValue = await encryptedStore.Get("device1");
            Assert.True(retrievedValue.HasValue);
            Assert.Equal(deviceNew.GenId, retrievedValue.OrDefault().GenId);
            Assert.Equal(deviceNew.Auth.Key, retrievedValue.OrDefault().Auth.Key);

            storedValue = await entityStore.Get("device1");
            Assert.True(storedValue.HasValue);

            string encryptedDeviceJson = Convert.ToBase64String(Encoding.UTF8.GetBytes(deviceNew.ToJson()));
            var encryptedData = new UpdatableEncryptedStore<string, TestDevice>.EncryptedData(true, encryptedDeviceJson);
            Assert.Equal(encryptedData.ToJson(), storedValue.OrDefault());

            retrievedValue = await encryptedStore.Get("device2");
            Assert.False(retrievedValue.HasValue);

            await encryptedStore.Remove("device1");
            contains = await encryptedStore.Contains("device1");
            Assert.False(contains);

            retrievedValue = await encryptedStore.Get("device1");
            Assert.False(retrievedValue.HasValue);
        }

        [Fact]
        public async Task BatchTest()
        {
            // Arrange
            IEncryptionProvider encryptionProvider = new TestEncryptionProvider();
            IEntityStore<string, string> entityStore = GetEntityStore<string, string>("smokeTest");
            IKeyValueStore<string, TestDevice> encryptedStore = new UpdatableEncryptedStore<string, TestDevice>(entityStore, encryptionProvider);
            IDictionary<string, TestDevice> devices = new Dictionary<string, TestDevice>();
            for (int i = 0; i < 10; i++)
            {
                devices[$"d{i}"] = new TestDevice(Guid.NewGuid().ToString(), new KeyAuth(Guid.NewGuid().ToString()));
            }

            // Act
            foreach (KeyValuePair<string, TestDevice> device in devices)
            {
                await encryptedStore.Put(device.Key, device.Value);
            }

            IDictionary<string, TestDevice> obtainedDevices = new Dictionary<string, TestDevice>();
            await encryptedStore.IterateBatch(
                10,
                (key, device) =>
                {
                    obtainedDevices[key] = device;
                    return Task.CompletedTask;
                });

            // Assert
            Assert.Equal(devices.Count, obtainedDevices.Count);

            foreach (KeyValuePair<string, TestDevice> device in devices)
            {
                Assert.Equal(device.Value.GenId, obtainedDevices[device.Key].GenId);
                Assert.Equal(device.Value.Auth.Key, obtainedDevices[device.Key].Auth.Key);
            }

            // Act
            Option<(string key, TestDevice value)> first = await encryptedStore.GetFirstEntry();
            Option<(string key, TestDevice value)> last = await encryptedStore.GetLastEntry();

            // Assert
            Assert.True(first.HasValue);
            Assert.True(last.HasValue);

            Assert.Equal("d0", first.OrDefault().key);
            Assert.Equal(devices["d0"].GenId, first.OrDefault().value.GenId);
            Assert.Equal("d9", last.OrDefault().key);
            Assert.Equal(devices["d9"].GenId, last.OrDefault().value.GenId);
        }

        [Fact]
        public async Task UpdatedBatchTest()
        {
            // Arrange
            IEncryptionProvider encryptionProvider = new TestEncryptionProvider();
            IEntityStore<string, string> entityStore = GetEntityStore<string, string>("smokeTest");
            IKeyValueStore<string, TestDevice> encryptedStore = new UpdatableEncryptedStore<string, TestDevice>(entityStore, encryptionProvider);
            IDictionary<string, TestDevice> devices = new Dictionary<string, TestDevice>();
            for (int i = 0; i < 10; i++)
            {
                devices[$"d{i}"] = new TestDevice(Guid.NewGuid().ToString(), new KeyAuth(Guid.NewGuid().ToString()));
            }

            // Act
            foreach (KeyValuePair<string, TestDevice> device in devices)
            {
                await entityStore.Put(device.Key, device.Value.ToJson());
            }

            IDictionary<string, TestDevice> obtainedDevices = new Dictionary<string, TestDevice>();
            await encryptedStore.IterateBatch(
                10,
                (key, device) =>
                {
                    obtainedDevices[key] = device;
                    return Task.CompletedTask;
                });

            // Assert
            Assert.Equal(devices.Count, obtainedDevices.Count);

            foreach (KeyValuePair<string, TestDevice> device in devices)
            {
                Assert.Equal(device.Value.GenId, obtainedDevices[device.Key].GenId);
                Assert.Equal(device.Value.Auth.Key, obtainedDevices[device.Key].Auth.Key);
            }

            // Act
            Option<(string key, TestDevice value)> first = await encryptedStore.GetFirstEntry();
            Option<(string key, TestDevice value)> last = await encryptedStore.GetLastEntry();

            // Assert
            Assert.True(first.HasValue);
            Assert.True(last.HasValue);

            Assert.Equal("d0", first.OrDefault().key);
            Assert.Equal(devices["d0"].GenId, first.OrDefault().value.GenId);
            Assert.Equal("d9", last.OrDefault().key);
            Assert.Equal(devices["d9"].GenId, last.OrDefault().value.GenId);
        }

        [Fact]
        public async Task KeyResetTest()
        {
            // Arrange
            var encryptionProvider = new ResettableEncryptionProvider();
            IEntityStore<string, string> entityStore = GetEntityStore<string, string>("smokeTest");
            IKeyValueStore<string, TestDevice> encryptedStore = new EncryptedStore<string, TestDevice>(entityStore, encryptionProvider);
            string key = "device1";
            var device = new TestDevice(Guid.NewGuid().ToString(), new KeyAuth(Guid.NewGuid().ToString()));

            // Act / Assert
            bool contains = await encryptedStore.Contains("device1");
            Assert.False(contains);

            await encryptedStore.Put(key, device);

            contains = await encryptedStore.Contains("device1");
            Assert.True(contains);

            Option<TestDevice> retrievedValue = await encryptedStore.Get("device1");
            Assert.True(retrievedValue.HasValue);
            Assert.Equal(device.GenId, retrievedValue.OrDefault().GenId);
            Assert.Equal(device.Auth.Key, retrievedValue.OrDefault().Auth.Key);

            encryptionProvider.Reset();

            retrievedValue = await encryptedStore.Get("device1");
            Assert.False(retrievedValue.HasValue);

            contains = await encryptedStore.Contains("device1");
            Assert.False(contains);

            await encryptedStore.Put(key, device);

            contains = await encryptedStore.Contains("device1");
            Assert.True(contains);

            retrievedValue = await encryptedStore.Get("device1");
            Assert.True(retrievedValue.HasValue);
            Assert.Equal(device.GenId, retrievedValue.OrDefault().GenId);
            Assert.Equal(device.Auth.Key, retrievedValue.OrDefault().Auth.Key);
        }

        [Fact]
        public async Task BatchWithKeyResetTest()
        {
            // Arrange
            var encryptionProvider = new ResettableEncryptionProvider();
            IEntityStore<string, string> entityStore = GetEntityStore<string, string>("smokeTest");
            IKeyValueStore<string, TestDevice> encryptedStore = new EncryptedStore<string, TestDevice>(entityStore, encryptionProvider);
            IDictionary<string, TestDevice> devices = new Dictionary<string, TestDevice>();
            for (int i = 0; i < 10; i++)
            {
                devices[$"d{i}"] = new TestDevice(Guid.NewGuid().ToString(), new KeyAuth(Guid.NewGuid().ToString()));
            }

            // Act
            foreach (KeyValuePair<string, TestDevice> device in devices)
            {
                await encryptedStore.Put(device.Key, device.Value);
            }

            IDictionary<string, TestDevice> obtainedDevices = new Dictionary<string, TestDevice>();
            await encryptedStore.IterateBatch(
                10,
                (key, device) =>
                {
                    obtainedDevices[key] = device;
                    return Task.CompletedTask;
                });

            // Assert
            Assert.Equal(devices.Count, obtainedDevices.Count);

            foreach (KeyValuePair<string, TestDevice> device in devices)
            {
                Assert.Equal(device.Value.GenId, obtainedDevices[device.Key].GenId);
                Assert.Equal(device.Value.Auth.Key, obtainedDevices[device.Key].Auth.Key);
            }

            // Act
            Option<(string key, TestDevice value)> first = await encryptedStore.GetFirstEntry();
            Option<(string key, TestDevice value)> last = await encryptedStore.GetLastEntry();

            // Assert
            Assert.True(first.HasValue);
            Assert.True(last.HasValue);

            Assert.Equal("d0", first.OrDefault().key);
            Assert.Equal(devices["d0"].GenId, first.OrDefault().value.GenId);
            Assert.Equal("d9", last.OrDefault().key);
            Assert.Equal(devices["d9"].GenId, last.OrDefault().value.GenId);

            // Act
            encryptionProvider.Reset();

            obtainedDevices = new Dictionary<string, TestDevice>();
            await encryptedStore.IterateBatch(
                10,
                (key, device) =>
                {
                    obtainedDevices[key] = device;
                    return Task.CompletedTask;
                });

            // Assert
            Assert.Equal(0, obtainedDevices.Count);

            // Act
            first = await encryptedStore.GetFirstEntry();
            last = await encryptedStore.GetLastEntry();

            // Assert
            Assert.False(first.HasValue);
            Assert.False(last.HasValue);

            // Act
            foreach (KeyValuePair<string, TestDevice> device in devices)
            {
                await encryptedStore.Put(device.Key, device.Value);
            }

            obtainedDevices = new Dictionary<string, TestDevice>();
            await encryptedStore.IterateBatch(
                10,
                (key, device) =>
                {
                    obtainedDevices[key] = device;
                    return Task.CompletedTask;
                });

            // Assert
            Assert.Equal(devices.Count, obtainedDevices.Count);

            foreach (KeyValuePair<string, TestDevice> device in devices)
            {
                Assert.Equal(device.Value.GenId, obtainedDevices[device.Key].GenId);
                Assert.Equal(device.Value.Auth.Key, obtainedDevices[device.Key].Auth.Key);
            }

            // Act
            first = await encryptedStore.GetFirstEntry();
            last = await encryptedStore.GetLastEntry();

            // Assert
            Assert.True(first.HasValue);
            Assert.True(last.HasValue);

            Assert.Equal("d0", first.OrDefault().key);
            Assert.Equal(devices["d0"].GenId, first.OrDefault().value.GenId);
            Assert.Equal("d9", last.OrDefault().key);
            Assert.Equal(devices["d9"].GenId, last.OrDefault().value.GenId);
        }

        static IEntityStore<TK, TV> GetEntityStore<TK, TV>(string entityName)
            => new EntityStore<TK, TV>(new KeyValueStoreMapper<TK, byte[], TV, byte[]>(new InMemoryDbStore(entityName), new BytesMapper<TK>(), new BytesMapper<TV>()), entityName);

        public class KeyAuth
        {
            [JsonConstructor]
            public KeyAuth(string key)
            {
                this.Key = key;
            }

            [JsonProperty("key")]
            public string Key { get; }
        }

        class TestDevice
        {
            [JsonConstructor]
            public TestDevice(string genId, KeyAuth auth)
            {
                this.GenId = genId;
                this.Auth = auth;
            }

            [JsonProperty("genId")]
            public string GenId { get; }

            [JsonProperty("auth")]
            public KeyAuth Auth { get; }
        }

        class TestEncryptionProvider : IEncryptionProvider
        {
            public Task<string> DecryptAsync(string encryptedText) => Task.FromResult(Encoding.UTF8.GetString(Convert.FromBase64String(encryptedText)));

            public Task<string> EncryptAsync(string plainText) => Task.FromResult(Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText)));
        }

        class ResettableEncryptionProvider : IEncryptionProvider
        {
            IDictionary<string, string> encryptedVals = new ConcurrentDictionary<string, string>();

            public Task<string> DecryptAsync(string encryptedText)
            {
                if (!this.encryptedVals.TryGetValue(encryptedText, out string plainText))
                {
                    return Task.FromException<string>(new InvalidOperationException("Error decrypting"));
                }

                return Task.FromResult(plainText);
            }

            public Task<string> EncryptAsync(string plainText)
            {
                string encryptedValue = Guid.NewGuid().ToString();
                this.encryptedVals.Add(encryptedValue, plainText);
                return Task.FromResult(encryptedValue);
            }

            public void Reset() => this.encryptedVals = new ConcurrentDictionary<string, string>();
        }
    }
}
