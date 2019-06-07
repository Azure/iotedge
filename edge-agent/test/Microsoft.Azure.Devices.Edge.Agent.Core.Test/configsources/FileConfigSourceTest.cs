// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.ConfigSources
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Xunit;

    public class FileConfigSourceTest : IDisposable
    {
        const string TestType = "test";

        const string ValidJson1 = @"{
                ""version"": 0,
                ""deploymentConfig"": { 
                    ""schemaVersion"": ""1.0"",
                    ""runtime"": { 
                      ""type"": ""test"", 
                      ""settings"": {
                        ""minDockerVersion"": ""v1.13"", 
                        ""loggingOptions"": """" 
                      }
                    },
                    ""systemModules"": {
                      ""edgeAgent"": { 
                        ""type"": ""test"",
                        ""imagePullPolicy"": ""on-create"",
                        ""settings"": {
                          ""image"": ""edge-agent""
                        },
                        ""configuration"": {
                          ""id"": ""1234""
                        }
                      },
                      ""edgeHub"": { 
                        ""type"": ""test"", 
                        ""status"": ""running"", 
                        ""restartPolicy"": ""always"",
                        ""imagePullPolicy"": ""on-create"",
                        ""settings"": {
                          ""image"": ""edge-hub:latest""
                        },
                        ""configuration"": {
                          ""id"":  ""1234""
                        }
                      }
                    },
                    ""modules"": {
                      ""mod1"": {
                        ""version"": ""version1"",
                        ""type"": ""test"",
                        ""status"": ""running"",
                        ""restartPolicy"": ""on-unhealthy"",
                        ""imagePullPolicy"": ""never"",
                        ""settings"": {
                          ""image"": ""image1""
                        },
                        ""configuration"": {
                          ""id"": ""1234""
                        }
                    }
                  }
                }
            }";

        const string ValidJson2 = @"{
                ""version"": 0,
                ""deploymentConfig"": { 
                    ""schemaVersion"": ""1.0"",
                    ""runtime"": { 
                      ""type"": ""test"", 
                      ""settings"": {
                        ""minDockerVersion"": ""v1.13"", 
                        ""loggingOptions"": """" 
                      }
                    },
                    ""systemModules"": {
                      ""edgeAgent"": { 
                        ""type"": ""test"",
                        ""imagePullPolicy"": ""on-create"",
                        ""settings"": {
                          ""image"": ""edge-agent""
                        },
                        ""configuration"": {
                          ""id"": ""1234""
                        }
                      },
                      ""edgeHub"": { 
                        ""type"": ""test"", 
                        ""status"": ""running"", 
                        ""restartPolicy"": ""always"",
                        ""imagePullPolicy"": ""on-create"",
                        ""settings"": {
                          ""image"": ""edge-hub:latest""
                        },
                        ""configuration"": {
                          ""id"":  ""1234""
                        }
                      }
                    },
                    ""modules"": {
                      ""mod1"": {
                        ""version"": ""version1"",
                        ""type"": ""test"",
                        ""status"": ""stopped"",
                        ""restartPolicy"": ""on-unhealthy"",
                        ""imagePullPolicy"": ""never"",
                        ""settings"": {
                          ""image"": ""image1""
                        },
                        ""configuration"": {
                          ""id"": ""1234""
                        }
                      },
                      ""mod2"": {
                        ""version"": ""version1"",
                        ""type"": ""test"",
                        ""status"": ""running"",
                        ""restartPolicy"": ""on-unhealthy"",
                        ""imagePullPolicy"": ""on-create"",
                        ""settings"": {
                          ""image"": ""image1""
                        },
                        ""configuration"": {
                          ""id"": ""1234""
                        }
                    }
                  }
                }
            }";

        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();

        static readonly ConfigurationInfo ConfigurationInfo = new ConfigurationInfo();

        static readonly TestConfig Config1 = new TestConfig("image1");

        static readonly IModule ValidModule1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.Never, ConfigurationInfo, EnvVars);

        static readonly IEdgeHubModule EdgeHubModule = new TestHubModule("edgeHub", "test", ModuleStatus.Running, new TestConfig("edge-hub:latest"), RestartPolicy.Always, ImagePullPolicy.OnCreate, ConfigurationInfo, EnvVars);

        static readonly IEdgeAgentModule EdgeAgentModule = new TestAgentModule("edgeAgent", "test", new TestConfig("edge-agent"), ImagePullPolicy.OnCreate, ConfigurationInfo, null);

        static readonly IDictionary<string, IModule> Modules1 = new Dictionary<string, IModule> { ["mod1"] = ValidModule1 };

        static readonly ModuleSet ValidSet1 = new ModuleSet(new Dictionary<string, IModule>(Modules1) { [EdgeHubModule.Name] = EdgeHubModule, [EdgeAgentModule.Name] = EdgeAgentModule });

        static readonly IModule UpdatedModule1 = new TestModule("mod1", "version1", "test", ModuleStatus.Stopped, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.Never, ConfigurationInfo, EnvVars);

        static readonly IModule ValidModule2 = new TestModule("mod2", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, ConfigurationInfo, EnvVars);

        static readonly IDictionary<string, IModule> Modules2 = new Dictionary<string, IModule> { ["mod1"] = UpdatedModule1, ["mod2"] = ValidModule2 };

        static readonly ModuleSet ValidSet2 = new ModuleSet(new Dictionary<string, IModule>(Modules2) { [EdgeHubModule.Name] = EdgeHubModule, [EdgeAgentModule.Name] = EdgeAgentModule });

        static readonly string InvalidJson1 = "{\"This is a terrible string\"}";

        readonly string tempFileName;

        readonly IConfigurationRoot config;

        readonly ISerde<DeploymentConfigInfo> serde;

        public FileConfigSourceTest()
        {
            // GetTempFileName() creates the file.
            this.tempFileName = Path.GetTempFileName();
            this.config = new ConfigurationBuilder().Build();

            var moduleDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestModule)
            };

            var edgeAgentDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestAgentModule)
            };

            var edgeHubDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestHubModule)
            };

            var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestRuntimeInfo)
            };

            var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
            {
                [typeof(IModule)] = moduleDeserializerTypes,
                [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
            };
            this.serde = new TypeSpecificSerDe<DeploymentConfigInfo>(deserializerTypesMap);
        }

        public void Dispose()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }
        }

        [Fact]
        [Unit]
        public async void CreateSuccess()
        {
            File.WriteAllText(this.tempFileName, ValidJson1);

            using (FileConfigSource configSource = await FileConfigSource.Create(this.tempFileName, this.config, this.serde))
            {
                Assert.NotNull(configSource);
                DeploymentConfigInfo deploymentConfigInfo = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(deploymentConfigInfo);
                Assert.NotNull(deploymentConfigInfo.DeploymentConfig);
                ModuleSet moduleSet = deploymentConfigInfo.DeploymentConfig.GetModuleSet();
                Diff emptyDiff = ValidSet1.Diff(moduleSet);
                Assert.True(emptyDiff.IsEmpty);
            }
        }

        [Fact]
        [Unit]
        public async void ChangeFileAndSeeChange()
        {
            // Set up initial config file and create `FileConfigSource`
            File.WriteAllText(this.tempFileName, ValidJson1);
            Diff validDiff1To2 = ValidSet2.Diff(ValidSet1);

            using (FileConfigSource configSource = await FileConfigSource.Create(this.tempFileName, this.config, this.serde))
            {
                Assert.NotNull(configSource);

                DeploymentConfigInfo deploymentConfigInfo = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(deploymentConfigInfo);
                ModuleSet initialModuleSet = deploymentConfigInfo.DeploymentConfig.GetModuleSet();
                Diff emptyDiff = ValidSet1.Diff(initialModuleSet);
                Assert.True(emptyDiff.IsEmpty);

                // Modify the config file by writing new content.
                File.WriteAllText(this.tempFileName, ValidJson2);
                await Task.Delay(TimeSpan.FromSeconds(20));

                DeploymentConfigInfo updatedAgentConfig = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(updatedAgentConfig);
                ModuleSet updatedModuleSet = updatedAgentConfig.DeploymentConfig.GetModuleSet();
                Diff newDiff = updatedModuleSet.Diff(initialModuleSet);
                Assert.False(newDiff.IsEmpty);
                Assert.Equal(newDiff, validDiff1To2);
            }
        }

        [Fact]
        [Unit]
        public async void ChangeFileToInvalidBackToOk()
        {
            // Set up initial config file and create `FileConfigSource`
            File.WriteAllText(this.tempFileName, ValidJson1);
            Diff validDiff1To2 = ValidSet2.Diff(ValidSet1);

            using (FileConfigSource configSource = await FileConfigSource.Create(this.tempFileName, this.config, this.serde))
            {
                Assert.NotNull(configSource);

                DeploymentConfigInfo deploymentConfigInfo = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(deploymentConfigInfo);
                ModuleSet initialModuleSet = deploymentConfigInfo.DeploymentConfig.GetModuleSet();
                Diff emptyDiff = ValidSet1.Diff(initialModuleSet);
                Assert.True(emptyDiff.IsEmpty);

                // Modify the config file by writing new content.
                File.WriteAllText(this.tempFileName, InvalidJson1);
                await Task.Delay(TimeSpan.FromSeconds(10));

                DeploymentConfigInfo updatedAgentConfig = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(updatedAgentConfig);
                ModuleSet updatedModuleSet = updatedAgentConfig.DeploymentConfig.GetModuleSet();
                Diff newDiff = updatedModuleSet.Diff(initialModuleSet);
                Assert.True(newDiff.IsEmpty);

                // Modify the config file by writing new content.
                File.WriteAllText(this.tempFileName, ValidJson2);
                await Task.Delay(TimeSpan.FromSeconds(10));

                updatedAgentConfig = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(updatedAgentConfig);
                updatedModuleSet = updatedAgentConfig.DeploymentConfig.GetModuleSet();
                newDiff = updatedModuleSet.Diff(initialModuleSet);
                Assert.False(newDiff.IsEmpty);
                Assert.Equal(newDiff, validDiff1To2);
            }
        }
    }

    class TestRuntimeInfo : IRuntimeInfo
    {
        public TestRuntimeInfo(string type)
        {
            this.Type = type;
        }

        public string Type { get; }

        public static bool operator ==(TestRuntimeInfo left, TestRuntimeInfo right) => Equals(left, right);

        public static bool operator !=(TestRuntimeInfo left, TestRuntimeInfo right) => !Equals(left, right);

        public bool Equals(IRuntimeInfo other) => other is TestRuntimeInfo otherRuntimeInfo
                                                  && this.Equals(otherRuntimeInfo);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return this.Equals((TestRuntimeInfo)obj);
        }

        public override int GetHashCode()
        {
            return this.Type != null ? this.Type.GetHashCode() : 0;
        }

        public bool Equals(TestRuntimeInfo other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.Type, other.Type);
        }
    }
}
