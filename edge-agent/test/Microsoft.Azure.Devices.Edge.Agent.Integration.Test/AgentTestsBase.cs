// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Integration.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunners;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Reporters;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public abstract class AgentTestsBase
    {
        const string TestConfigBasePath = "test-configs";

        public static IEnumerable<object[]> GenerateStartTestData(string testConfig)
        {
            Type agentTestsType = typeof(AgentTestsBase);
            string format = $"{agentTestsType.Namespace}.{{0}}, {agentTestsType.Assembly.GetName().Name}";

            string testConfigPath = Path.Combine(TestConfigBasePath, testConfig);
            string json = File.ReadAllText(testConfigPath);
            var settings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new TypeNameSerializationBinder(format),
                Converters = new List<JsonConverter>
                {
                    new ModuleSetJsonConverter(),
                    new DeploymentConfigJsonConverter()
                }
            };

            return JsonConvert.DeserializeObject<TestConfig[]>(json, settings).Select(config => new object[] { config });
        }

        protected async Task AgentExecutionTestAsync(TestConfig testConfig)
        {
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                        { "DeviceConnectionString", $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}" }
                }).Build();

            var deploymentConfigInfo = new DeploymentConfigInfo(1, testConfig.DeploymentConfig);

            var configSource = new Mock<IConfigSource>();
            configSource.Setup(cs => cs.Configuration).Returns(configRoot);
            configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).ReturnsAsync(deploymentConfigInfo);
            NullReporter reporter = NullReporter.Instance;

            var restartStateStore = Mock.Of<IEntityStore<string, ModuleState>>();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var deploymentConfigInfoSerde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            IRestartPolicyManager restartManager = new Mock<IRestartPolicyManager>().Object;

            var environment = new Mock<IEnvironment>();
            environment.Setup(e => e.GetModulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(testConfig.RuntimeInfo);
            environment.Setup(e => e.GetRuntimeInfoAsync()).ReturnsAsync(UnknownRuntimeInfo.Instance);

            var environmentProvider = new Mock<IEnvironmentProvider>();
            environmentProvider.Setup(ep => ep.Create(It.IsAny<DeploymentConfig>())).Returns(environment.Object);

            var commandFactory = new TestCommandFactory();

            var credential = new ConnectionStringCredentials("fake");
            IDictionary<string, IModuleIdentity> identities = new Dictionary<string, IModuleIdentity>();

            var identity = new Mock<IModuleIdentity>();
            identity.Setup(id => id.Credentials).Returns(credential);
            identity.Setup(id => id.ModuleId).Returns(Constants.EdgeAgentModuleName);
            identities.Add(Constants.EdgeAgentModuleName, identity.Object);

            if (testConfig.DeploymentConfig.SystemModules.EdgeHub.HasValue)
            {
                identity = new Mock<IModuleIdentity>();
                identity.Setup(id => id.Credentials).Returns(credential);
                identity.Setup(id => id.ModuleId).Returns(Constants.EdgeHubModuleName);
                identities.Add(Constants.EdgeHubModuleName, identity.Object);
            }

            foreach (var module in testConfig.DeploymentConfig.Modules)
            {
                identity = new Mock<IModuleIdentity>();
                identity.Setup(id => id.Credentials).Returns(credential);
                identity.Setup(id => id.ModuleId).Returns(module.Key);
                identities.Add(module.Key, identity.Object);
            }

            foreach (var module in testConfig.RuntimeInfo.Modules)
            {
                if (identities.ContainsKey(module.Key))
                {
                    continue;
                }

                identity = new Mock<IModuleIdentity>();
                identity.Setup(id => id.Credentials).Returns(credential);
                identity.Setup(id => id.ModuleId).Returns(module.Key);
                identities.Add(module.Key, identity.Object);
            }

            IImmutableDictionary<string, IModuleIdentity> immutableIdentities = identities.ToImmutableDictionary();
            var moduleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            moduleIdentityLifecycleManager.Setup(m => m.GetModuleIdentitiesAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>())).Returns(Task.FromResult(immutableIdentities));
            var availabilityMetric = Mock.Of<IDeploymentMetrics>();

            var store = Mock.Of<IEntityStore<string, ModuleState>>();
            HealthRestartPlanner restartPlanner = new HealthRestartPlanner(commandFactory, store, TimeSpan.FromSeconds(10), restartManager);

            Agent agent = await Agent.Create(
                configSource.Object,
                restartPlanner,
                new OrderedPlanRunner(),
                reporter,
                moduleIdentityLifecycleManager.Object,
                environmentProvider.Object,
                configStore,
                deploymentConfigInfoSerde,
                NullEncryptionProvider.Instance,
                availabilityMetric);
            await agent.ReconcileAsync(CancellationToken.None);
            Assert.True(testConfig.Validator.Validate(commandFactory));
        }

        class TypeSpecificJsonConverter : JsonConverter
        {
            readonly IDictionary<Type, IDictionary<string, Type>> deserializerTypesMap;

            public TypeSpecificJsonConverter(IDictionary<Type, IDictionary<string, Type>> deserializerTypesMap)
            {
                this.deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>();
                foreach (KeyValuePair<Type, IDictionary<string, Type>> deserializerTypes in deserializerTypesMap)
                {
                    this.deserializerTypesMap[deserializerTypes.Key] = new Dictionary<string, Type>(deserializerTypes.Value, StringComparer.OrdinalIgnoreCase);
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                // The null check is required to gracefully handle a null object
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }

                if (!this.deserializerTypesMap.TryGetValue(objectType, out IDictionary<string, Type> deserializerTypeMap))
                {
                    throw new JsonSerializationException($"Could not find type {objectType.Name} in deserializerTypeMap");
                }

                JObject obj = JObject.Load(reader);
                var converterType = obj.Get<JToken>("type");

                if (!deserializerTypeMap.TryGetValue(converterType.Value<string>(), out Type serializeType))
                {
                    throw new JsonSerializationException($"Could not find right converter given a type {converterType.Value<string>()}");
                }

                object deserializedObject = JsonConvert.DeserializeObject(obj.ToString(), serializeType);
                return deserializedObject;
            }

            public override bool CanConvert(Type objectType) =>
                this.deserializerTypesMap.ContainsKey(objectType);
        }

        class DeploymentConfigJsonConverter : JsonConverter
        {
            private readonly JsonSerializerSettings settings;

            public DeploymentConfigJsonConverter()
            {
                var moduleDeserializerTypes = new Dictionary<string, Type>
                {
                    { "docker", typeof(DockerDesiredModule) }
                };

                var edgeAgentDeserializerTypes = new Dictionary<string, Type>
                {
                    { "docker", typeof(EdgeAgentDockerModule) }
                };

                var edgeHubDeserializerTypes = new Dictionary<string, Type>
                {
                    { "docker", typeof(EdgeHubDockerModule) }
                };

                var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
                {
                    { "docker", typeof(DockerRuntimeInfo) }
                };

                var deserializerTypes = new Dictionary<Type, IDictionary<string, Type>>
                {
                    [typeof(IModule)] = moduleDeserializerTypes,
                    [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                    [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                    [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
                };

                this.settings = new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Converters = new List<JsonConverter>
                    {
                        new TypeSpecificJsonConverter(deserializerTypes)
                    }
                };
            }

            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JObject obj = JObject.Load(reader);
                return JsonConvert.DeserializeObject<DeploymentConfig>(obj.ToString(), this.settings);
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(DeploymentConfig);
        }

        class ModuleSetJsonConverter : JsonConverter
        {
            private readonly JsonSerializerSettings settings;

            public ModuleSetJsonConverter()
            {
                var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
                {
                    ["docker"] = typeof(DockerReportedRuntimeInfo),
                    [Constants.Unknown] = typeof(UnknownRuntimeInfo)
                };

                var edgeAgentDeserializerTypes = new Dictionary<string, Type>
                {
                    ["docker"] = typeof(EdgeAgentDockerRuntimeModule),
                    [Constants.Unknown] = typeof(UnknownEdgeAgentModule)
                };

                var edgeHubDeserializerTypes = new Dictionary<string, Type>
                {
                    ["docker"] = typeof(EdgeHubDockerRuntimeModule),
                    [Constants.Unknown] = typeof(UnknownEdgeHubModule)
                };

                var moduleDeserializerTypes = new Dictionary<string, Type>
                {
                    ["docker"] = typeof(DockerRuntimeModule)
                };

                var deserializerTypes = new Dictionary<Type, IDictionary<string, Type>>
                    {
                        { typeof(IRuntimeInfo), runtimeInfoDeserializerTypes },
                        { typeof(IEdgeAgentModule), edgeAgentDeserializerTypes },
                        { typeof(IEdgeHubModule), edgeHubDeserializerTypes },
                        { typeof(IModule), moduleDeserializerTypes }
                    };

                this.settings = new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Converters = new List<JsonConverter>
                    {
                        new TypeSpecificJsonConverter(deserializerTypes)
                    }
                };
            }

            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JObject obj = JObject.Load(reader);
                Dictionary<string, IModule> modules = new Dictionary<string, IDictionary<string, IModule>>(JsonConvert.DeserializeObject<IDictionary<string, IDictionary<string, IModule>>>(obj.ToString(), this.settings), StringComparer.OrdinalIgnoreCase)
                    .GetOrElse("modules", new Dictionary<string, IModule>())
                    .ToDictionary(
                        pair => pair.Key,
                        pair =>
                        {
                            pair.Value.Name = pair.Key;
                            return pair.Value;
                        });

                return new ModuleSet(modules);
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(ModuleSet);
        }
    }
}
