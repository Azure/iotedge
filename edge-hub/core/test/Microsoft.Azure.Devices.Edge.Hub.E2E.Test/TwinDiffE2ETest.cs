// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class TwinDiffE2ETest
    {
        const string DeviceNamePrefix = "E2E_twin_";

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task AddPropertySuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new Twin();
                    twinPatch.Properties.Desired = new TwinCollection
                    {
                        ["101"] = "value"
                    };

                    (TwinCollection, TwinCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");

                    twinPatch.Properties.Desired = new TwinCollection
                    {
                        ["101"] = "value",
                        ["101-new"] = new TwinCollection
                        {
                            ["object"] = "value"
                        }
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");
                });
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task OverwritePropertySuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new Twin();
                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["102"] = "value"
                    };

                    (TwinCollection, TwinCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");

                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["102"] = "overwritten value"
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");
                });
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task UnchangedPropertySuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new Twin();
                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["103"] = new TwinCollection()
                        {
                            ["object"] = new TwinCollection()
                            {
                                ["object"] = new TwinCollection()
                                {
                                    ["103"] = "value"
                                }
                            }
                        }
                    };

                    (TwinCollection, TwinCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");

                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["103"] = new TwinCollection()
                        {
                            ["object"] = new TwinCollection()
                            {
                                ["object"] = new TwinCollection()
                                {
                                    ["103"] = "value",
                                }
                            }
                        }
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");
                });
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task RemovePropertySuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new Twin();
                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["104"] = new TwinCollection()
                        {
                            ["object"] = new TwinCollection()
                            {
                                ["object"] = new TwinCollection()
                                {
                                    ["104"] = "value"
                                }
                            }
                        }
                    };

                    (TwinCollection, TwinCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");

                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["104"] = new TwinCollection()
                        {
                            ["object"] = new TwinCollection()
                            {
                                ["object"] = new TwinCollection()
                                {
                                    ["104"] = null,
                                }
                            }
                        }
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");
                });
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task NonexistantRemovePropertySuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new Twin();
                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["105"] = new TwinCollection()
                        {
                            ["object"] = new TwinCollection()
                            {
                                ["object"] = new TwinCollection()
                                {
                                    ["105"] = "value"
                                }
                            }
                        }
                    };

                    (TwinCollection, TwinCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");

                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["105"] = new TwinCollection()
                        {
                            ["object"] = new TwinCollection()
                            {
                                ["object"] = new TwinCollection()
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
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");
                });
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task OverwriteValueWithObjectSuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new Twin();
                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["106"] = "value"
                    };

                    (TwinCollection, TwinCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");

                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["106"] = new TwinCollection()
                        {
                            ["object"] = new TwinCollection()
                            {
                                ["object"] = new TwinCollection()
                                {
                                    ["106"] = "value"
                                }
                            }
                        }
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");
                });
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task OverwriteObjectWithValueSuccess(ITransportSettings[] transportSettings)
        {
            await this.RunTestCase(
                transportSettings,
                async (deviceClient, deviceName, registryManager) =>
                {
                    var twinPatch = new Twin();
                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["107"] = new TwinCollection()
                        {
                            ["object"] = new TwinCollection()
                            {
                                ["object"] = new TwinCollection()
                                {
                                    ["107"] = "value"
                                }
                            }
                        }
                    };

                    (TwinCollection, TwinCollection) results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");

                    twinPatch.Properties.Desired = new TwinCollection()
                    {
                        ["107"] = "value"
                    };

                    results = await this.TestTwinUpdate(deviceClient, deviceName, registryManager, twinPatch);

                    Assert.True(
                        JToken.DeepEquals(
                            JToken.Parse(results.Item1.ToJson()),
                            JToken.Parse(results.Item2.ToJson())),
                        $"result.Item1={results.Item1.ToJson()}, result.Item2={results.Item2.ToJson()}");
                });
        }

        static async Task<(DeviceClient, string)> InitializeDeviceClient(RegistryManager rm, string iotHubConnectionString, ITransportSettings[] settings)
        {
            DeviceClient deviceClient = null;
            (string deviceName, string deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, rm);
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, settings);
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

            return (deviceClient, deviceName);
        }

        async Task RunTestCase(ITransportSettings[] transportSettings, Func<DeviceClient, string, RegistryManager, Task> testCase)
        {
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            RegistryManager rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            (DeviceClient deviceClient, string deviceName) = await InitializeDeviceClient(rm, iotHubConnectionString, transportSettings);
            try
            {
                await testCase(deviceClient, deviceName, rm);
            }
            finally
            {
                await deviceClient.CloseAsync();
                await RegistryManagerHelper.RemoveDevice(deviceName, rm);
                await rm.CloseAsync();
            }
        }

        async Task<(TwinCollection, TwinCollection)> TestTwinUpdate(
            DeviceClient deviceClient,
            string deviceName,
            RegistryManager rm,
            Twin twinPatch)
        {
            var receivedDesiredProperties = new TwinCollection();
            bool desiredPropertiesUpdateCallbackTriggered = false;

            Task DesiredPropertiesUpdateCallback(TwinCollection desiredproperties, object usercontext)
            {
                receivedDesiredProperties = desiredproperties;
                desiredPropertiesUpdateCallbackTriggered = true;
                return Task.CompletedTask;
            }

            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertiesUpdateCallback, null);

            // fetch the newly minted twin
            Twin originalCloudTwin = await deviceClient.GetTwinAsync();
            Twin rmTwin = await rm.GetTwinAsync(deviceName);

            // updated twin in the cloud with the patch
            await rm.UpdateTwinAsync(deviceName, twinPatch, rmTwin.ETag);

            // Get the updated twin
            Twin updatedCloudTwin = await deviceClient.GetTwinAsync();

            // replicate the patch operation locally
            var delayTask = Task.Delay(TimeSpan.FromSeconds(60));
            while (!desiredPropertiesUpdateCallbackTriggered && !delayTask.IsCompleted)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }

            string mergedJson = JsonEx.Merge(originalCloudTwin.Properties.Desired, receivedDesiredProperties, true);
            var localMergedTwinProperties = new TwinCollection(mergedJson);

            return (localMergedTwinProperties, updatedCloudTwin.Properties.Desired);
        }
    }
}
