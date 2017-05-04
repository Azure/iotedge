// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Xunit;

    public class AgentTests
    {
        [Theory]
        [Bvt]
        [MemberData(nameof(GenerateStartTestData))]
        public async Task AgentStartsUpModules(TestConfig testConfig)
        {
            ILoggerFactory loggerFactory = new LoggerFactory()
                .AddConsole();

            // Build the docker host URL.
            string dockerHostUrl = ConfigHelper.AppConfig["dockerHostUrl"];
            string dockerPort = ConfigHelper.AppConfig["dockerHostPort"];
            if (string.IsNullOrEmpty(dockerPort) == false)
            {
                dockerHostUrl += $":{dockerPort}";
            }

            DockerClient client = new DockerClientConfiguration(new Uri(dockerHostUrl)).CreateClient();

            try
            {
                // Remove any running containers with the same name that may be a left-over
                // from previous test runs.
                await RemoveContainer(client, testConfig);

                // Initialize docker configuration for this module.
                DockerConfig dockerConfig = testConfig.PortBindings != null
                    ? new DockerConfig(testConfig.ImageName, testConfig.ImageTag, testConfig.PortBindings)
                    : new DockerConfig(testConfig.ImageName, testConfig.ImageTag);

                // Initialize an Edge Agent module object.
                var dockerModule = new DockerModule(
                    testConfig.Name,
                    testConfig.Version,
                    ModuleStatus.Running,
                    dockerConfig
                );
                ModuleSet moduleSet = ModuleSet.Create(dockerModule);

                // Start up the agent and run a "reconcile".
                var dockerCommandFactory = new DockerCommandFactory(client);
                var environment = new DockerEnvironment(client);
                var commandFactory = new LoggingCommandFactory(dockerCommandFactory, loggerFactory);
                var agent = new Agent(moduleSet, environment, new RestartPlanner(commandFactory));
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

                Assert.Equal(true, validated);
            }
            finally
            {
                await RemoveContainer(client, testConfig);
            }
        }

        private static async Task RemoveContainer(IDockerClient client, TestConfig testConfig)
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

        public static IEnumerable<object[]> GenerateStartTestData()
        {
            IEnumerable<IConfigurationSection> testsToRun = ConfigHelper.AppConfig.GetSection("testSuite").GetChildren();

            // Each test in the test suite supports the notion of a "validator". We determine what
            // validator to use by looking at the "$type" property in the test configuration JSON.
            // Here's an example:
            //
            //      {
            //         "name": "mongo-server",
            //         "version": "1.0",
            //         "imageName": "mongo",
            //         "imageTag": "3.4.4",
            //         "validator": {
            //             "$type": "RunCommandValidator",
            //             "command": "docker",
            //             "args": "run --rm --link mongo-server:mongo-server mongo:3.4.4 sh -c \"exec mongo --quiet --eval 'db.serverStatus().version' mongo-server:27017/test\"",
            //             "outputEquals": "3.4.4"
            //         }
            //      }
            // 
            // Here the value "RunCommandValidator" for "$type" means that Newtonsoft JSON will
            // de-serialize the "validator" object from the JSON into an instance of type "RunCommandValidator".
            // We provide the mapping from the value of "$type" to a fully qualified .NET type name by providing
            // a "serialization binder" - in our case this is an instance of TypeNameSerializationBinder. The JSON
            // deserializer consults the TypeNameSerializationBinder instance to determine what type to instantiate.

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
    }
}
