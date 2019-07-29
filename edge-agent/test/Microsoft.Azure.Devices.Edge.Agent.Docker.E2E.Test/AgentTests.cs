// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunners;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Reporters;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class AgentTests
    {
        public static IEnumerable<object[]> GenerateStartTestData()
        {
            IEnumerable<IConfigurationSection> testsToRun = ConfigHelper.TestConfig.GetSection("testSuite").GetChildren();

            // Each test in the test suite supports the notion of a "validator". We determine what
            // validator to use by looking at the "$type" property in the test configuration JSON.
            // Here's an example:
            //
            //      {
            //         "name": "mongo-server",
            //         "version": "1.0",
            //         "image": "mongo:3.4.4",
            //         "imageCreateOptions": "{\"HostConfig\": {\"PortBindings\": {\"80/tcp\": [{\"HostPort\": \"8080\"}]}}}",
            //         "imagePullPolicyTestConfig": {
            //             "imagePullPolicy": "on-create",
            //             "pullImage": "false"
            //         },
            //         "validator": {
            //             "$type": "RunCommandValidator",
            //             "command": "docker",
            //             "args": "run --rm --link mongo-server:mongo-server mongo:3.4.4 sh -c \"exec mongo --quiet --eval 'db.serverStatus().version' mongo-server:27017/test\"",
            //             "exitCode": 0,
            //             "outputEquals": "3.4.4"
            //         }
            //      }
            //
            // Here the value "RunCommandValidator" for "$type" means that Newtonsoft JSON will
            // de-serialize the "validator" object from the JSON into an instance of type "RunCommandValidator".
            // We provide the mapping from the value of "$type" to a fully qualified .NET type name by providing
            // a "serialization binder" - in our case this is an instance of TypeNameSerializationBinder. The JSON
            // deserializer consults the TypeNameSerializationBinder instance to determine what type to instantiate.
            //
            // The "pullPolicyTestConfig" configuration is optional. It's intended for cases where we wish to test
            // the behavior of the Agent based on the pull policy specified for a module.
            //
            // The "exitCode" configuration is optional. By default it's expected value is assumed to be 0.
            Type agentTestsType = typeof(AgentTests);
            string format = $"{agentTestsType.Namespace}.{{0}}, {agentTestsType.Assembly.GetName().Name}";
            var settings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new TypeNameSerializationBinder(format)
            };

            // appSettings.json contains an array property called "testSuite" which is a list of strings
            // containing names of JSON files that contain the spec for the tests to run. We process all the
            // JSON files and build a flat list of tests (instances of TestConfig).
            IEnumerable<object[]> result = testsToRun.SelectMany(
                cs =>
                {
                    string json = File.ReadAllText(cs.Value);
                    return JsonConvert
                        .DeserializeObject<TestConfig[]>(json, settings)
                        .Select(config => new object[] { config });
                });

            return result;
        }

        [Integration]
        [Theory]
        [MemberData(nameof(GenerateStartTestData))]
        public async Task AgentStartsUpModules(TestConfig testConfig)
        {
            // Build the docker host URL.
            string dockerHostUrl = ConfigHelper.TestConfig["dockerHostUrl"];
            DockerClient client = new DockerClientConfiguration(new Uri(dockerHostUrl)).CreateClient();

            try
            {
                // Remove any running containers with the same name that may be a left-over
                // from previous test runs.
                await RemoveContainer(client, testConfig);

                // Remove old images and pull a new image if specified in the test config.
                await PullImage(client, testConfig);

                // Initialize docker configuration for this module.
                DockerConfig dockerConfig = testConfig.ImageCreateOptions != null
                    ? new DockerConfig(testConfig.Image, testConfig.ImageCreateOptions)
                    : new DockerConfig(testConfig.Image);

                ImagePullPolicy imagePullPolicy = ImagePullPolicy.OnCreate;
                testConfig.ImagePullPolicyTestConfig.ForEach(p => imagePullPolicy = p.ImagePullPolicy);

                // Initialize an Edge Agent module object.
                var dockerModule = new DockerModule(
                    testConfig.Name,
                    testConfig.Version,
                    ModuleStatus.Running,
                    global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.OnUnhealthy,
                    dockerConfig,
                    imagePullPolicy,
                    null,
                    null);
                var modules = new Dictionary<string, IModule> { [testConfig.Name] = dockerModule };
                var systemModules = new SystemModules(null, null);

                // Start up the agent and run a "reconcile".
                var dockerLoggingOptions = new Dictionary<string, string>
                {
                    { "max-size", "1m" },
                    { "max-file", "1" }
                };
                var loggingConfig = new DockerLoggingConfig("json-file", dockerLoggingOptions);

                string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
                IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                    new Dictionary<string, string>
                    {
                        { "DeviceConnectionString", $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}" }
                    }).Build();

                var runtimeConfig = new DockerRuntimeConfig("1.24.0", "{}");
                var runtimeInfo = new DockerRuntimeInfo("docker", runtimeConfig);
                var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", runtimeInfo, systemModules, modules));

                var configSource = new Mock<IConfigSource>();
                configSource.Setup(cs => cs.Configuration).Returns(configRoot);
                configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).ReturnsAsync(deploymentConfigInfo);

                // TODO: Fix this up with a real reporter. But before we can do that we need to use
                // the real configuration source that talks to IoT Hub above.
                NullReporter reporter = NullReporter.Instance;

                var restartStateStore = Mock.Of<IEntityStore<string, ModuleState>>();
                var configStore = Mock.Of<IEntityStore<string, string>>();
                var deploymentConfigInfoSerde = Mock.Of<ISerde<DeploymentConfigInfo>>();
                IRestartPolicyManager restartManager = new Mock<IRestartPolicyManager>().Object;

                var dockerCommandFactory = new DockerCommandFactory(client, loggingConfig, configSource.Object, new CombinedDockerConfigProvider(Enumerable.Empty<AuthConfig>()));
                IRuntimeInfoProvider runtimeInfoProvider = await RuntimeInfoProvider.CreateAsync(client);
                IEnvironmentProvider environmentProvider = await DockerEnvironmentProvider.CreateAsync(runtimeInfoProvider, restartStateStore, restartManager);

                var logFactoryMock = new Mock<ILoggerFactory>();
                var logMock = new Mock<ILogger<LoggingCommandFactory>>();
                logFactoryMock.Setup(l => l.CreateLogger(It.IsAny<string>()))
                    .Returns(logMock.Object);
                var commandFactory = new LoggingCommandFactory(dockerCommandFactory, logFactoryMock.Object);

                var credential = new ConnectionStringCredentials("fake");
                var identity = new Mock<IModuleIdentity>();
                identity.Setup(id => id.Credentials).Returns(credential);
                identity.Setup(id => id.ModuleId).Returns(testConfig.Name);
                IImmutableDictionary<string, IModuleIdentity> identities = new Dictionary<string, IModuleIdentity>()
                {
                    [testConfig.Name] = identity.Object
                }.ToImmutableDictionary();
                var moduleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
                moduleIdentityLifecycleManager.Setup(m => m.GetModuleIdentitiesAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>())).Returns(Task.FromResult(identities));

                Agent agent = await Agent.Create(
                    configSource.Object,
                    new RestartPlanner(commandFactory),
                    new OrderedPlanRunner(),
                    reporter,
                    moduleIdentityLifecycleManager.Object,
                    environmentProvider,
                    configStore,
                    deploymentConfigInfoSerde,
                    NullEncryptionProvider.Instance);
                await agent.ReconcileAsync(CancellationToken.None);

                // Sometimes the container is still not ready by the time we run the validator.
                // So we attempt validation multiple times and bail only if all of them fail.
                bool validated = false;
                int attempts = 0;
                const int MaxAttempts = 5;
                while (!validated && attempts < MaxAttempts)
                {
                    validated = testConfig.Validator.Validate();
                    if (!validated)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    }

                    ++attempts;
                }

                Assert.True(validated);
            }
            finally
            {
                await RemoveContainer(client, testConfig);
            }
        }

        static async Task RemoveContainer(IDockerClient client, TestConfig testConfig)
        {
            // get current list of containers (running or otherwise) where their name
            // matches what's given in the test settings
            IList<ContainerListResponse> containersList = await client.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = true
                });
            IEnumerable<ContainerListResponse> toBeRemoved = containersList
                .Where(c => c.Names.Contains($"/{testConfig.Name}"));

            // blow them away!
            var removeParams = new ContainerRemoveParameters
            {
                Force = true
            };
            await Task.WhenAll(toBeRemoved.Select(c => client.Containers.RemoveContainerAsync(c.ID, removeParams)));
        }

        static async Task PullImage(IDockerClient client, TestConfig testConfig)
        {
            // First, delete the image if it's already present.
            IList<ImagesListResponse> images = await client.Images.ListImagesAsync(
                new ImagesListParameters
                {
                    MatchName = testConfig.Image,
                });

            foreach (ImagesListResponse image in images)
            {
                await client.Images.DeleteImageAsync(
                    image.ID,
                    new ImageDeleteParameters
                    {
                        Force = true,
                    });
            }

            bool pullImage = false;
            testConfig.ImagePullPolicyTestConfig.ForEach(p => pullImage = p.PullImage);

            // Pull the image if the test config specifies that the image should be pulled.
            if (pullImage)
            {
                await client.Images.CreateImageAsync(
                    new ImagesCreateParameters
                    {
                        FromImage = testConfig.Image,
                    },
                    new AuthConfig(),
                    new Progress<JSONMessage>());
            }
        }
    }
}
