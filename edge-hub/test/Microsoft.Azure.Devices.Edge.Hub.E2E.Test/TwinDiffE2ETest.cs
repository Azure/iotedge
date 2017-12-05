// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    [Bvt]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class TwinDiffE2ETest
    {
        ProtocolHeadFixture head = ProtocolHeadFixture.GetInstance();
        const string DeviceNamePrefix = "E2E_twin_";
        string deviceName;
        RegistryManager rm = null;
        DeviceClient deviceClient = null;
        string deviceConnectionString;

        [Fact, TestPriority(101)]
        public async void AddPropertySuccess()
        {
            var twinPatch = new Twin();
            twinPatch.Properties.Desired = new TwinCollection()
            {
                ["101"] = "value"
            };

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, false);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));

            twinPatch.Properties.Desired = new TwinCollection()
            {
                ["101"] = "value",
                ["101-new"] = new TwinCollection()
                {
                    ["object"] = "value"
                }
            };

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(102)]
        public async void OverwritePropertySuccess()
        {
            var twinPatch = new Twin();
            twinPatch.Properties.Desired = new TwinCollection()
            {
                ["102"] = "value"
            };

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, false);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));

            twinPatch.Properties.Desired = new TwinCollection()
            {
                ["102"] = "overwritten value"
            };

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(103)]
        public async void UnchangedPropertySuccess()
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

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, false);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));

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

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(104)]
        public async void RemovePropertySuccess()
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

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, false);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));

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

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(105)]
        public async void NonexistantRemovePropertySuccess()
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

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, false);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));

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

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(106)]
        public async void OverwriteValueWithObjectSuccess()
        {
            var twinPatch = new Twin();
            twinPatch.Properties.Desired = new TwinCollection()
            {
                ["106"] = "value"
            };

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, false);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));

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

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(107)]
        public async void OverwriteObjectWithValueSuccess()
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

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, false);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));

            twinPatch.Properties.Desired = new TwinCollection()
            {
                ["107"] = "value"
            };

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        async Task Setup(DesiredPropertyUpdateCallback callback, Twin twinPatch, Func<Twin, Task> afterSetup, Func<Task> afterCallback)
        {
            if (this.rm == null)
            {
                string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

                this.rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
                (this.deviceName, this.deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, this.rm);

                var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                };
                ITransportSettings[] settings = { mqttSetting };
                this.deviceClient = DeviceClient.CreateFromConnectionString(this.deviceConnectionString, settings);
                await this.deviceClient.OpenAsync();
            }
            await this.deviceClient.SetDesiredPropertyUpdateCallbackAsync(callback, afterCallback);
            await afterSetup(twinPatch);
        }

        async Task Teardown()
        {
            await this.deviceClient.CloseAsync();
            await RegistryManagerHelper.RemoveDevice(this.deviceName, this.rm);
            await this.rm.CloseAsync();
            this.rm = null;
        }

        async Task<Tuple<TwinCollection, TwinCollection>> RunTestCase(CancellationTokenSource cts, Twin twinPatch, bool teardown)
        {
            var desiredPropertyPatch = new TwinCollection();
            var originalCloudTwin = new Twin();
            var updatedCloudTwin = new Twin();
            var localMergedTwinProperties = new TwinCollection();

            await this.Setup(async (diff, ctx) =>
            {
                desiredPropertyPatch = diff;
                var next = (Func<Task>)ctx;
                await next();
                cts.Cancel();
            },
            twinPatch,
            async (p) => // after setup
            {
                // fetch the newly minted twin
                originalCloudTwin = await this.deviceClient.GetTwinAsync();

                Twin rmTwin = await this.rm.GetTwinAsync(this.deviceName);

                // updated twin in the cloud with the patch
                await this.rm.UpdateTwinAsync(this.deviceName, p, rmTwin.ETag);
            },
            async () => // after callback
            {
                updatedCloudTwin = await this.deviceClient.GetTwinAsync();

                // replicate the patch operation locally
                string mergedJson = JsonEx.Merge(originalCloudTwin.Properties.Desired, desiredPropertyPatch, true);
                localMergedTwinProperties = new TwinCollection(mergedJson);
            });

            while (!cts.IsCancellationRequested)
            {
                Thread.Sleep(10000);
            }

            if (teardown)
            {
                await this.Teardown();
            }

            cts.Dispose();

            return new Tuple<TwinCollection, TwinCollection>(localMergedTwinProperties, updatedCloudTwin.Properties.Desired);
        }
    }
}
