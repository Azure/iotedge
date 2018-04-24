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

    [E2E]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class TwinDiffE2ETest : IClassFixture<ProtocolHeadFixture>
    {
        static readonly ITransportSettings[] MqttTransportSettings =
        {
            new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            }
        };

        static readonly ITransportSettings[] AmqpTransportSettings =
        {
            new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            }
        };

        const string DeviceNamePrefix = "E2E_twin_";
        string deviceName;
        RegistryManager rm;
        DeviceClient deviceClient;
        string deviceConnectionString;

        [Fact, TestPriority(101)]
        public Task AddPropertySuccess_Amqp() => AddPropertySuccess(AmqpTransportSettings);

        [Fact, TestPriority(102)]
        public Task AddPropertySuccess_Mqtt() => AddPropertySuccess(MqttTransportSettings); 

        async Task AddPropertySuccess(ITransportSettings[] transportSettings)
        {
            var twinPatch = new Twin();
            twinPatch.Properties.Desired = new TwinCollection()
            {
                ["101"] = "value"
            };

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, false);

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

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(103)]
        public Task OverwritePropertySuccess_Amqp() => this.OverwritePropertySuccess(AmqpTransportSettings);

        [Fact, TestPriority(104)]
        public Task OverwritePropertySuccess_Mqtt() => this.OverwritePropertySuccess(MqttTransportSettings); 

        async Task OverwritePropertySuccess(ITransportSettings[] transportSettings)
        {
            var twinPatch = new Twin();
            twinPatch.Properties.Desired = new TwinCollection()
            {
                ["102"] = "value"
            };

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, false);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));

            twinPatch.Properties.Desired = new TwinCollection()
            {
                ["102"] = "overwritten value"
            };

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(105)]
        public Task UnchangedPropertySuccess_Amqp() => this.UnchangedPropertySuccess(AmqpTransportSettings); 

        [Fact, TestPriority(106)]
        public Task UnchangedPropertySuccess_Mqtt() => this.UnchangedPropertySuccess(MqttTransportSettings);

        async Task UnchangedPropertySuccess(ITransportSettings[] transportSettings)
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

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, false);

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

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(107)]
        public Task RemovePropertySuccess_Amqp() => this.RemovePropertySuccess(AmqpTransportSettings);

        [Fact, TestPriority(108)]
        public Task RemovePropertySuccess_Mqtt() => this.RemovePropertySuccess(MqttTransportSettings); 

        public async Task RemovePropertySuccess(ITransportSettings[] transportSettings)
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

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, false);

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

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(109)]
        public Task NonexistantRemovePropertySuccess_Amqp() => this.NonexistantRemovePropertySuccess(AmqpTransportSettings);

        [Fact, TestPriority(110)]
        public Task NonexistantRemovePropertySuccess_Mqtt() => this.NonexistantRemovePropertySuccess(MqttTransportSettings); 

        async Task NonexistantRemovePropertySuccess(ITransportSettings[] transportSettings)
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

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, false);

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

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(111)]
        public Task OverwriteValueWithObjectSuccess_Mqtt() => this.OverwriteValueWithObjectSuccess(MqttTransportSettings);

        [Fact, TestPriority(112)]
        public Task OverwriteValueWithObjectSuccess_Amqp() => this.OverwriteValueWithObjectSuccess(AmqpTransportSettings);

        async Task OverwriteValueWithObjectSuccess(ITransportSettings[] transportSettings)
        {
            var twinPatch = new Twin();
            twinPatch.Properties.Desired = new TwinCollection()
            {
                ["106"] = "value"
            };

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, false);

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

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        [Fact, TestPriority(113)]
        public Task OverwriteObjectWithValueSuccess_Mqtt() => this.OverwriteObjectWithValueSuccess(MqttTransportSettings);

        [Fact, TestPriority(114)]
        public Task OverwriteObjectWithValueSuccess_Amqp() => this.OverwriteObjectWithValueSuccess(AmqpTransportSettings);

        async Task OverwriteObjectWithValueSuccess(ITransportSettings[] transportSettings)
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

            Tuple<TwinCollection, TwinCollection> results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, transportSettings, false);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));

            twinPatch.Properties.Desired = new TwinCollection()
            {
                ["107"] = "value"
            };

            results = await this.RunTestCase(new CancellationTokenSource(), twinPatch, MqttTransportSettings, true);

            Assert.True(JToken.DeepEquals(
                    JToken.Parse(results.Item1.ToJson()),
                    JToken.Parse(results.Item2.ToJson())));
        }

        async Task Setup(DesiredPropertyUpdateCallback callback, Twin twinPatch, Func<Twin, Task> afterSetup, Func<Task> afterCallback, ITransportSettings[] settings)
        {
            if (this.rm == null)
            {
                string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

                this.rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
                (this.deviceName, this.deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, this.rm);

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

        async Task<Tuple<TwinCollection, TwinCollection>> RunTestCase(CancellationTokenSource cts, Twin twinPatch, ITransportSettings[] transportSettings, bool teardown)
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
            },
            transportSettings);

            while (!cts.IsCancellationRequested)
            {
                Thread.Sleep(10000);
            }

            if (teardown)
            {
                await this.Teardown();
            }

            return new Tuple<TwinCollection, TwinCollection>(localMergedTwinProperties, updatedCloudTwin.Properties.Desired);
        }
    }
}
