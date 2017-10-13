// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Microsoft.Azure.Devices.Client;
	using Microsoft.Azure.Devices.Client.Transport.Mqtt;
	using Microsoft.Azure.Devices.Edge.Util.Test;
	using Microsoft.Azure.Devices.Edge.Util.Test.Common;
	using Xunit;
	using Message = Microsoft.Azure.Devices.Message;
	using Microsoft.Azure.Devices.Shared;
	using Microsoft.Azure.Devices.Edge.Hub.Core;
	using Newtonsoft.Json.Linq;
	using Newtonsoft.Json;
	using Microsoft.Azure.Devices.Edge.Util.Concurrency;
	using System.Threading;

	[Bvt]
	[Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
	[TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
	public class TwinDiffE2ETest
	{
		ProtocolHeadFixture head = ProtocolHeadFixture.GetInstance();
		const string MessagePropertyName = "property1";
		const string DeviceNamePrefix = "E2E_twin_";
		string deviceName;
		RegistryManager rm = null;
		DeviceClient deviceClient = null;
		string deviceConnectionString;

		[Fact, TestPriority(101)]
		public async void AddPropertySuccess()
		{
			Twin twinPatch = new Twin();
			twinPatch.Properties.Desired = new TwinCollection()
			{
				["101"] = "value"
			};

			Tuple<TwinCollection, TwinCollection> results = await RunTestCase(new CancellationTokenSource(), twinPatch, false);

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

			results = await RunTestCase(new CancellationTokenSource(), twinPatch, true);

			Assert.True(JToken.DeepEquals(
					JToken.Parse(results.Item1.ToJson()),
					JToken.Parse(results.Item2.ToJson())));
		}

		[Fact, TestPriority(102)]
		public async void OverwritePropertySuccess()
		{
			Twin twinPatch = new Twin();
			twinPatch.Properties.Desired = new TwinCollection()
			{
				["102"] = "value"
			};

			Tuple<TwinCollection, TwinCollection> results = await RunTestCase(new CancellationTokenSource(), twinPatch, false);

			Assert.True(JToken.DeepEquals(
					JToken.Parse(results.Item1.ToJson()),
					JToken.Parse(results.Item2.ToJson())));

			twinPatch.Properties.Desired = new TwinCollection()
			{
				["102"] = "overwritten value"
			};

			results = await RunTestCase(new CancellationTokenSource(), twinPatch, true);

			Assert.True(JToken.DeepEquals(
					JToken.Parse(results.Item1.ToJson()),
					JToken.Parse(results.Item2.ToJson())));
		}

		[Fact, TestPriority(103)]
		public async void UnchangedPropertySuccess()
		{
			Twin twinPatch = new Twin();
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

			Tuple<TwinCollection, TwinCollection> results = await RunTestCase(new CancellationTokenSource(), twinPatch, false);

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

			results = await RunTestCase(new CancellationTokenSource(), twinPatch, true);

			Assert.True(JToken.DeepEquals(
					JToken.Parse(results.Item1.ToJson()),
					JToken.Parse(results.Item2.ToJson())));
		}

		[Fact, TestPriority(104)]
		public async void RemovePropertySuccess()
		{
			Twin twinPatch = new Twin();
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

			Tuple<TwinCollection, TwinCollection> results = await RunTestCase(new CancellationTokenSource(), twinPatch, false);

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

			results = await RunTestCase(new CancellationTokenSource(), twinPatch, true);

			Assert.True(JToken.DeepEquals(
					JToken.Parse(results.Item1.ToJson()),
					JToken.Parse(results.Item2.ToJson())));
		}

		[Fact, TestPriority(105)]
		public async void NonexistantRemovePropertySuccess()
		{
			Twin twinPatch = new Twin();
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

			Tuple<TwinCollection, TwinCollection> results = await RunTestCase(new CancellationTokenSource(), twinPatch, false);

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

			results = await RunTestCase(new CancellationTokenSource(), twinPatch, true);

			Assert.True(JToken.DeepEquals(
					JToken.Parse(results.Item1.ToJson()),
					JToken.Parse(results.Item2.ToJson())));
		}

		[Fact, TestPriority(106)]
		public async void OverwriteValueWithObjectSuccess()
		{
			Twin twinPatch = new Twin();
			twinPatch.Properties.Desired = new TwinCollection()
			{
				["106"] = "value"
			};

			Tuple<TwinCollection, TwinCollection> results = await RunTestCase(new CancellationTokenSource(), twinPatch, false);

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

			results = await RunTestCase(new CancellationTokenSource(), twinPatch, true);

			Assert.True(JToken.DeepEquals(
					JToken.Parse(results.Item1.ToJson()),
					JToken.Parse(results.Item2.ToJson())));
		}

		[Fact, TestPriority(107)]
		public async void OverwriteObjectWithValueSuccess()
		{
			Twin twinPatch = new Twin();
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

			Tuple<TwinCollection, TwinCollection> results = await RunTestCase(new CancellationTokenSource(), twinPatch, false);

			Assert.True(JToken.DeepEquals(
					JToken.Parse(results.Item1.ToJson()),
					JToken.Parse(results.Item2.ToJson())));

			twinPatch.Properties.Desired = new TwinCollection()
			{
				["107"] = "value"
			};

			results = await RunTestCase(new CancellationTokenSource(), twinPatch, true);

			Assert.True(JToken.DeepEquals(
					JToken.Parse(results.Item1.ToJson()),
					JToken.Parse(results.Item2.ToJson())));
		}

		async Task Setup(DesiredPropertyUpdateCallback callback, Twin twinPatch, Func<Twin, Task> afterSetup, Func<Task> afterCallback)
		{
			if (rm == null)
			{
				string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

				rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
				(deviceName, deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, rm);

				var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
				{
					RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
				};
				ITransportSettings[] settings = { mqttSetting };
				deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, settings);
				await deviceClient.OpenAsync();
			}
			await deviceClient.SetDesiredPropertyUpdateCallbackAsync(callback, afterCallback);
			await afterSetup(twinPatch);
		}

		async Task Teardown()
		{
			await deviceClient.CloseAsync();
			await RegistryManagerHelper.RemoveDevice(deviceName, rm);
			await rm.CloseAsync();
			rm = null;
		}

		async Task<Tuple<TwinCollection, TwinCollection>> RunTestCase(CancellationTokenSource cts, Twin twinPatch, bool teardown)
		{
			TwinCollection desiredPropertyPatch = new TwinCollection();
			Twin originalCloudTwin = new Twin();
			Twin updatedCloudTwin = new Twin();
			TwinCollection localMergedTwinProperties = new TwinCollection();

			await Setup(async (diff, ctx) =>
			{
				desiredPropertyPatch = diff;
				Func<Task> next = (Func<Task>)ctx;
				await next();
				cts.Cancel();
			},
			twinPatch,
			async (p) => // after setup
			{
				// fetch the newly minted twin
				originalCloudTwin = await deviceClient.GetTwinAsync();

				Twin rmTwin = await rm.GetTwinAsync(deviceName);

				// updated twin in the cloud with the patch
				await rm.UpdateTwinAsync(deviceName, p, rmTwin.ETag);
			},
			async () => // after callback
			{
				updatedCloudTwin = await deviceClient.GetTwinAsync();

				// replicate the patch operation locally
				localMergedTwinProperties = TwinManager.MergeTwinCollections(originalCloudTwin.Properties.Desired, desiredPropertyPatch, true);
			});

			while (!cts.IsCancellationRequested)
			{
				Thread.Sleep(10000);
			}

			if (teardown)
			{
				await Teardown();
			}

			cts.Dispose();

			return new Tuple<TwinCollection, TwinCollection>(localMergedTwinProperties, updatedCloudTwin.Properties.Desired);
		}
	}
}