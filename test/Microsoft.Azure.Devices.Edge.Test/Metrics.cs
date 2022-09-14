// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Newtonsoft.Json;
    using NUnit.Framework;

    using ConfigModuleName = Microsoft.Azure.Devices.Edge.Test.Common.Config.ModuleName;

    [EndToEnd]
    public class Metrics : SasManualProvisioningFixture
    {
        public const string ValidatorModuleName = "metricsValidator";
        public const string CollectorModuleName = "metricsCollector";

        [Test]
        [Category("CentOsSafe")]
        public async Task MetricsCollector()
        {
            CancellationToken token = this.TestToken;

            string metricsCollectorImage = Context.Current.MetricsCollectorImage.Expect(() => new ArgumentException("metricsCollectorImage parameter is required for MetricsCollector test"));
            string hubResourceId = Context.Current.HubResourceId.Expect(() => new ArgumentException("IOT_HUB_RESOURCE_ID is required for MetricsCollector test"));

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(CollectorModuleName, metricsCollectorImage)
                        .WithEnvironment(new[]
                        {
                            ("UploadTarget", "IotMessage"),
                            ("ResourceID", hubResourceId),
                            ("ScrapeFrequencyInSecs", "10"),
                            ("CompressForUpload", "false")
                        });
                    builder.GetModule(ModuleName.EdgeHub)
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["routes"] = new
                            {
                                AzureIotEdgeMetricsCollectorToCloud = $"FROM /messages/modules/{CollectorModuleName}/* INTO $upstream"
                            }
                        });
                },
                token);

            EdgeModule azureIotEdgeMetricsCollector = deployment.Modules[CollectorModuleName];

            string output = await azureIotEdgeMetricsCollector.WaitForEventsReceivedAsync(DateTime.Now, token, "id");

            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.MissingMemberHandling = MissingMemberHandling.Error;

            List<IoTHubMetric> iotHubMetrics = new List<IoTHubMetric>() { };
            iotHubMetrics.AddRange(JsonConvert.DeserializeObject<IoTHubMetric[]>(output, settings));

            Assert.True(iotHubMetrics.Count > 0);
        }

        [Test]
        public async Task ValidateMetrics()
        {
            CancellationToken token = this.TestToken;
            await this.DeployAsync(token);

            var agent = new EdgeAgent(this.runtime.DeviceId, this.iotHub);
            await agent.PingAsync(token);

            var result = await this.iotHub.InvokeMethodAsync(
                this.runtime.DeviceId,
                ValidatorModuleName,
                new CloudToDeviceMethod(
                "ValidateMetrics",
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(60)),
                token);
            Assert.AreEqual(result.Status, (int)HttpStatusCode.OK);

            string body = result.GetPayloadAsJson();
            Report report = JsonConvert.DeserializeObject<Report>(body, new JsonSerializerSettings() { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });
            Assert.Zero(report.Failed, report.ToString());
        }

        async Task DeployAsync(CancellationToken token)
        {
            // First deploy everything needed for this test, including a temporary image that will be removed later to bump the "stopped" metric
            string metricsValidatorImage = Context.Current.MetricsValidatorImage.Expect(() => new InvalidOperationException("Missing Metrics Validator image"));
            await this.runtime.DeployConfigurationAsync(
                builder =>
                    {
                        builder.AddTemporaryModule();
                        builder.AddMetricsValidatorConfig(metricsValidatorImage);
                    }, token);

            // Next remove the temporary image from the deployment
            await this.runtime.DeployConfigurationAsync(
                builder => { builder.AddMetricsValidatorConfig(metricsValidatorImage); },
                token);
        }

        class IoTHubMetric
        {
            [JsonProperty("TimeGeneratedUtc")]
            public DateTime TimeGeneratedUtc { get; set; }
            [JsonProperty("Name")]
            public string Name { get; set; }
            [JsonProperty("Value")]
            public double Value { get; set; }
            [JsonProperty("Labels")]
            public IReadOnlyDictionary<string, string> Labels { get; set; }
        }

        // Presents a more focused view by serializing only failures
        public class Report
        {
            [JsonProperty]
            public string Category;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public TimeSpan Duration = TimeSpan.Zero;

            [JsonProperty]
            public List<string> Successes = new List<string>();

            [JsonProperty]
            public Dictionary<string, string> Failures = new Dictionary<string, string>();

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public List<Report> Subcategories = null;

            [JsonProperty(Order = -2)]
            public int Succeeded;

            [JsonProperty(Order = -2)]
            public int Failed;

            public bool ShouldSerializeSuccesses() => false;
            public bool ShouldSerializeFailures() => this.Failures.Any();
            public bool ShouldSerializeSubcategories() => this.Failed != 0;

            public override string ToString()
            {
                var settings = new JsonSerializerSettings()
                {
                    Converters = new List<JsonConverter>() { new Converter() },
                    Formatting = Formatting.Indented
                };

                return JsonConvert.SerializeObject(this, settings);
            }

            // Skips subcategories that don't have failures
            class Converter : JsonConverter
            {
                public override bool CanConvert(Type objectType) => objectType == typeof(List<Report>);
                public override bool CanRead => false;

                public override object ReadJson(JsonReader r, Type t, object o, JsonSerializer s) =>
                    throw new NotImplementedException();

                public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
                    serializer.Serialize(writer, ((List<Report>)value).Where(c => c.Failed != 0).ToArray());
            }
        }
    }

    static class EdgeConfigBuilderEx
    {
        public static void AddTemporaryModule(this EdgeConfigBuilder builder)
        {
            const string Name = "stopMe";
            string image = Context.Current.TempSensorImage.Expect(() => new InvalidOperationException("Missing Temp Sensor image"));
            builder.AddModule(Name, image).WithEnvironment(new[] { ("MessageCount", "0") });
        }

        public static void AddMetricsValidatorConfig(this EdgeConfigBuilder builder, string image)
        {
            builder.AddModule(Metrics.ValidatorModuleName, image);

            builder.GetModule(ConfigModuleName.EdgeHub)
                .WithDesiredProperties(new Dictionary<string, object>
                {
                    {
                        "routes", new
                        {
                            All = "FROM /messages/* INTO $upstream",
                            QueueLengthTest = $"FROM /messages/modules/{Metrics.ValidatorModuleName}/outputs/ToSelf INTO BrokeredEndpoint(\"/modules/{Metrics.ValidatorModuleName}/inputs/FromSelf\")"
                        }
                    }
                });

            builder.GetModule("$edgeAgent").WithEnvironment(("PerformanceMetricsUpdateFrequency", "00:00:20"));
        }
    }
}
