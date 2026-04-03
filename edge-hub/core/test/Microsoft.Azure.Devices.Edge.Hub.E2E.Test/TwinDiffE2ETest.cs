// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class TwinDiffE2ETest
    {
        const string DeviceNamePrefix = "E2E_twin_";

        [Theory(Skip = "Flaky")]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task AddPropertySuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new ClientTwin();
                    twinPatch.Properties.Desired = new PropertyCollection
                    {
                        ["101"] = "value"
                    };

                    (PropertyCollection, PropertyCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");

                    twinPatch.Properties.Desired = new PropertyCollection
                    {
                        ["101"] = "value",
                        ["101-new"] = new PropertyCollection
                        {
                            ["object"] = "value"
                        }
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");
                });
        }

        [Theory(Skip = "Flaky")]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task OverwritePropertySuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new ClientTwin();
                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["102"] = "value"
                    };

                    (PropertyCollection, PropertyCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");

                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["102"] = "overwritten value"
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");
                });
        }

        [Theory(Skip = "Flaky")]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task UnchangedPropertySuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new ClientTwin();
                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["103"] = new PropertyCollection()
                        {
                            ["object"] = new PropertyCollection()
                            {
                                ["object"] = new PropertyCollection()
                                {
                                    ["103"] = "value"
                                }
                            }
                        }
                    };

                    (PropertyCollection, PropertyCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");

                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["103"] = new PropertyCollection()
                        {
                            ["object"] = new PropertyCollection()
                            {
                                ["object"] = new PropertyCollection()
                                {
                                    ["103"] = "value",
                                }
                            }
                        }
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");
                });
        }

        [Theory(Skip = "Flaky")]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task RemovePropertySuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new ClientTwin();
                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["104"] = new PropertyCollection()
                        {
                            ["object"] = new PropertyCollection()
                            {
                                ["object"] = new PropertyCollection()
                                {
                                    ["104"] = "value"
                                }
                            }
                        }
                    };

                    (PropertyCollection, PropertyCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");

                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["104"] = new PropertyCollection()
                        {
                            ["object"] = new PropertyCollection()
                            {
                                ["object"] = new PropertyCollection()
                                {
                                    ["104"] = null,
                                }
                            }
                        }
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");
                });
        }

        [Theory(Skip = "Flaky")]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task NonexistantRemovePropertySuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new ClientTwin();
                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["105"] = new PropertyCollection()
                        {
                            ["object"] = new PropertyCollection()
                            {
                                ["object"] = new PropertyCollection()
                                {
                                    ["105"] = "value"
                                }
                            }
                        }
                    };

                    (PropertyCollection, PropertyCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");

                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["105"] = new PropertyCollection()
                        {
                            ["object"] = new PropertyCollection()
                            {
                                ["object"] = new PropertyCollection()
                                {
                                    ["105"] = "value",
                                    ["nonexistant"] = null
                                }
                            }
                        }
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");
                });
        }

        [Theory(Skip = "Flaky")]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task OverwriteValueWithObjectSuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new ClientTwin();
                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["106"] = "value"
                    };

                    (PropertyCollection, PropertyCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");

                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["106"] = new PropertyCollection()
                        {
                            ["object"] = new PropertyCollection()
                            {
                                ["object"] = new PropertyCollection()
                                {
                                    ["106"] = "value"
                                }
                            }
                        }
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");
                });
        }

        [Theory(Skip = "Flaky")]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task OverwriteObjectWithValueSuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new ClientTwin();
                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["107"] = new PropertyCollection()
                        {
                            ["object"] = new PropertyCollection()
                            {
                                ["object"] = new PropertyCollection()
                                {
                                    ["107"] = "value"
                                }
                            }
                        }
                    };

                    (PropertyCollection, PropertyCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");

                    twinPatch.Properties.Desired = new PropertyCollection()
                    {
                        ["107"] = "value"
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.GetSerializedString()),
                            JToken.Parse(results.Item2.GetSerializedString())),
                        $"result.Item1={results.Item1.GetSerializedString()}, result.Item2={results.Item2.GetSerializedString()}");
                });
        }

        static async Task<(IotHubDeviceClient, string)> InitializeDeviceClient(IotHubServiceClient rm, string iotHubConnectionString, ITransportSettings[] settings)
        {
            IotHubDeviceClient deviceClient = null;
            string edgeDeviceId = ConnectionStringHelper.GetDeviceId(ConfigHelper.TestConfig[Service.Constants.ConfigKey.IotHubConnectionString]);
            var edgeDevice = await rm.Devices.GetAsync(edgeDeviceId);
            (string deviceName, string deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, rm, scope: edgeDevice.Scope);
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    deviceClient = IotHubDeviceClient.CreateFromConnectionString(deviceConnectionString, settings);
                    await deviceClient.OpenAsync();
                    break;
                }
                catch (Exception)
                {
                    if (i == 2)
                    {
                        throw;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var device = await rm.Devices.GetAsync(deviceName);
                    if (device.ConnectionState != DeviceConnectionState.Connected)
                        throw new Exception("Device not connected to cloud");
                    break;
                }
                catch (Exception)
                {
                    if (i == 4)
                    {
                        throw;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            return (deviceClient, deviceName);
        }

        async Task RunTestCase(ITransportSettings[] transportSettings, Func<IotHubDeviceClient, string, IotHubServiceClient, Task> testCase)
        {
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubServiceClient rm = new IotHubServiceClient(iotHubConnectionString);
            (IotHubDeviceClient deviceClient, string deviceName) = await InitializeDeviceClient(rm, iotHubConnectionString, transportSettings);
            try
            {
                await testCase(deviceClient, deviceName, rm);
            }
            finally
            {
                await deviceClient.CloseAsync();
                await RegistryManagerHelper.RemoveDevice(deviceName, rm);
                rm.Dispose();
            }
        }

        async Task<(PropertyCollection, PropertyCollection)> TestTwinUpdate(
            IotHubDeviceClient deviceClient,
            string deviceName,
            IotHubServiceClient rm,
            ClientTwin twinPatch)
        {
            var receivedDesiredProperties = new PropertyCollection();
            bool desiredPropertiesUpdateCallbackTriggered = false;

            Task DesiredPropertiesUpdateCallback(PropertyCollection desiredproperties, object usercontext)
            {
                receivedDesiredProperties = desiredproperties;
                desiredPropertiesUpdateCallbackTriggered = true;
                return Task.CompletedTask;
            }

            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertiesUpdateCallback, null);

            // fetch the newly minted twin
            TwinProperties originalCloudTwin = await deviceClient.GetTwinAsync();
            ClientTwin rmTwin = await rm.Twins.GetAsync(deviceName);

            // updated twin in the cloud with the patch
            await rm.Twins.UpdateAsync(deviceName, twinPatch, true, CancellationToken.None);

            // Get the updated twin
            TwinProperties updatedCloudTwin = await deviceClient.GetTwinAsync();

            // replicate the patch operation locally
            var delayTask = Task.Delay(TimeSpan.FromSeconds(60));
            while (!desiredPropertiesUpdateCallbackTriggered && !delayTask.IsCompleted)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }

            string mergedJson = JsonEx.Merge(originalCloudTwin.Desired, receivedDesiredProperties, true);
            var localMergedTwinProperties = JsonConvert.DeserializeObject<PropertyCollection>(mergedJson);

            return (localMergedTwinProperties, updatedCloudTwin.Desired);
        }
    }
}
