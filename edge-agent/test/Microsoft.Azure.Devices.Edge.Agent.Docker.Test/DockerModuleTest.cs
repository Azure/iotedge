// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class DockerModuleTest
    {
        const string SerializedModule = @"{""version"":""version1"",""type"":""docker"",""status"":""running"",""restartPolicy"":""on-unhealthy"",""imagePullPolicy"":""on-create"",""settings"":{""image"":""image1:42"", ""createOptions"": {""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}], ""42/tcp"": [{""HostPort"": ""42""}]}}}},""configuration"":{""id"":""1""},""env"":{""Env1"": {""value"":""Val1""}}}";
        static readonly ConfigurationInfo DefaultConfigurationInfo = null;
        static readonly DockerConfig Config1 = new DockerConfig("image1:42", @"{""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}], ""42/tcp"": [{""HostPort"": ""42""}]}}}");
        static readonly DockerConfig Config2 = new DockerConfig("image2:42", @"{""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}], ""42/tcp"": [{""HostPort"": ""42""}]}}}");
        static readonly IDictionary<string, EnvVal> DefaultEnvVals = new Dictionary<string, EnvVal> { ["Env1"] = new EnvVal("Val1") };
        static readonly IModule Module1 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, DefaultEnvVals);
        static readonly IModule Module2 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, DefaultEnvVals);
        static readonly IModule Module3 = new DockerModule("mod3", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, DefaultEnvVals);
        static readonly IModule Module4 = new DockerModule("mod1", "version2", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, DefaultEnvVals);
        static readonly IModule Module6 = new DockerModule("mod1", "version1", ModuleStatus.Unknown, RestartPolicy.OnUnhealthy, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, DefaultEnvVals);
        static readonly IModule Module7 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config2, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, DefaultEnvVals);
        static readonly DockerModule Module8 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, DefaultEnvVals);
        static readonly IModule Module9 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, DefaultEnvVals);
        static readonly IModule Module10 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, new Dictionary<string, EnvVal> { ["Env1"] = new EnvVal("Val2") });
        static readonly IModule Module11 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, ImagePullPolicy.Never, DefaultConfigurationInfo, DefaultEnvVals);
        static readonly IModule ModuleWithConfig = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), DefaultEnvVals);
        static readonly DockerModule ValidJsonModule = new DockerModule("<module_name>", "<semantic_version_number>", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, DefaultEnvVals);

        static readonly JObject TestJsonInputs = JsonConvert.DeserializeObject<JObject>(
            @"
{
   ""invalidEnvJson"":[
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""settings"":{
            ""image"":""image1:ver1"",
            ""createoptions"":{
               ""env"":10
            }
         },
         ""configuration"": {
            ""id"": ""1""
         },
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""settings"":{
            ""image"":""image1:ver1"",
            ""createoptions"":{
               ""env"":""boo""
            }
         },
         ""configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""settings"":{
            ""image"":""image1:ver1"",
            ""createoptions"":{
               ""env"":true
            }
         },
         ""configuration"": {
            ""id"": ""1""
         }
      }
   ],
   ""validEnvJson"":[
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-failure"",
         ""imagePullPolicy"": ""on-create"",
         ""settings"":{
            ""image"":""image1:ver1"",
            ""createoptions"":{
               ""env"":[
                  ""k1=v1"",
                  ""k2=v2""
               ]
            }
         },
         ""configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""always"",
         ""imagePullPolicy"": ""on-create"",
         ""settings"":{
            ""image"":""image1:ver1"",
            ""createoptions"":{
               ""env"":[""""]
            }
         },
         ""configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""imagePullPolicy"": ""on-create"",
         ""settings"":{
            ""image"":""image1:ver1"",
            ""createoptions"":{
               ""env"":[]
            }
         },
         ""configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""never"",
         ""imagePullPolicy"": ""on-create"",
         ""settings"":{
            ""image"":""image1:ver1"",
         },
         ""configuration"": {
            ""id"": ""1""
         }
      }
   ],
   ""validJson"":[
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"": ""on-unhealthy"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:42"",
            ""CreateOptions"": {
               ""HostConfig"": {
                  ""PortBindings"": {
                     ""43/udp"": [
                        {
                           ""HostPort"": ""43""
                        }
                     ],
                     ""42/tcp"": [
                        {
                           ""HostPort"": ""42""
                        }
                     ]
                  }
               }
            }
         },
         ""Configuration"": {
            ""id"": ""1""
         },
        ""env"":{
            ""Env1"":{
              ""value"":""Val1""
            }
          }
      },
      {
         ""version"":""<semantic_version_number>"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""imagePullPolicy"": ""on-create"",
         ""settings"":{
            ""image"":""image1:42"",
            ""createoptions"": {
               ""hostconfig"": {
                  ""portbindings"": {
                     ""43/udp"": [
                        {
                           ""hostport"": ""43""
                        }
                     ],
                     ""42/tcp"": [
                        {
                           ""hostport"": ""42""
                        }
                     ]
                  }
               }
            }
         },
         ""configuration"": {
            ""id"": ""1""
         },
        ""env"":{
            ""Env1"":{
              ""value"":""Val1""
            }
          }
      },
      {
         ""VERSION"":""<semantic_version_number>"",
         ""TYPE"":""docker"",
         ""STATUS"":""running"",
         ""RESTARTPOLICY"": ""on-unhealthy"",
         ""IMAGEPULLPOLICY"": ""on-create"",
         ""SETTINGS"":{
            ""IMAGE"":""image1:42"",
            ""CREATEOPTIONS"": {
               ""HOSTCONFIG"": {
                  ""PORTBINDINGS"": {
                     ""43/udp"": [
                        {
                           ""HOSTPORT"": ""43""
                        }
                     ],
                     ""42/tcp"": [
                        {
                           ""HOSTPORT"": ""42""
                        }
                     ]
                  }
               }
            }
         },
         ""CONFIGURATION"": {
            ""id"": ""1""
         },
        ""env"":{
            ""Env1"":{
              ""value"":""Val1""
            }
          }
      }
   ],
   ""throwsException"":[      
      {
         ""Version"":""<semantic_version_number>"",
         ""Status"":""running"",
         ""Settings"":{
            ""Image"":""<docker_image_name>""
         },
         ""Configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""Configuration"": {
            ""id"": ""1""
         }
      }
   ],
   ""statusJson"":[
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""stopped"",
         ""Settings"":{
            ""Image"":""<docker_image_name>:42"",
         },
         ""Configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""Unknown"",
         ""Settings"":{
            ""Image"":""<docker_image_name>:42"",
         },
         ""Configuration"": {
            ""id"": ""1""
         }
      }
   ]
}
");

        public static IEnumerable<object[]> GetInvalidEnvJsonInputs()
        {
            return GetJsonTestCases("invalidEnvJson").Select(s => new object[] { s });
        }

        public static IEnumerable<object[]> GetValidJsonInputs()
        {
            return GetJsonTestCases("validJson").Select(s => new object[] { s });
        }

        public static IEnumerable<object[]> GetExceptionJsonInputs()
        {
            return GetJsonTestCases("throwsException").Select(s => new object[] { s });
        }

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, null, ImagePullPolicy.OnCreate, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerModule("mod1", "version1", (ModuleStatus)int.MaxValue, RestartPolicy.OnUnhealthy, Config1, ImagePullPolicy.OnCreate, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerModule("mod1", "version1", ModuleStatus.Running, (RestartPolicy)int.MaxValue, Config1, ImagePullPolicy.OnCreate, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, (ImagePullPolicy)int.MaxValue, null, null));
        }

        [Fact]
        [Unit]
        public void TestEquality()
        {
            Assert.Equal(Module1, Module1);
            Assert.Equal(Module1, Module2);
            Assert.Equal(Module8, Module8);
            Assert.NotEqual(Module1, Module3);
            Assert.NotEqual(Module1, Module9);
            Assert.NotEqual(Module1, Module11);
            Assert.NotEqual(Module1, Module4);
            Assert.NotEqual(Module1, Module6);
            Assert.NotEqual(Module1, Module7);
            Assert.Equal(Module1, Module8);
            Assert.Equal(Module9, ModuleWithConfig);
            Assert.NotEqual(Module1, Module10);

            Assert.False(Module1.Equals(null));
            Assert.False(Module8.Equals(null));

            Assert.True(Module1.Equals(Module1));
            Assert.False(Module1.Equals(Module3));

            Assert.False(Module1.Equals((object)null));
            Assert.False(Module8.Equals((object)null));
            Assert.True(Module1.Equals((object)Module1));
            Assert.True(Module1.Equals((object)Module2));
            Assert.False(Module1.Equals((object)Module3));
            Assert.False(Module1.Equals(new object()));
            Assert.True(Module1.Equals((IModule<DockerConfig>)Module1));

            Assert.Equal(Module1.GetHashCode(), Module2.GetHashCode());
            Assert.NotEqual(Module1.GetHashCode(), Module3.GetHashCode());
            Assert.NotEqual(Module1.GetHashCode(), Module9.GetHashCode());
            Assert.NotEqual(Module1.GetHashCode(), Module11.GetHashCode());
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetValidJsonInputs))]
        public void TestDeserializeValidJson(string inputJson)
        {
            var module = ModuleSerde.Instance.Deserialize<DockerModule>(inputJson);
            module.Name = "<module_name>";
            Assert.True(ValidJsonModule.Equals(module));
        }

        [Fact]
        [Unit]
        public void TestDeserializeStatusJson()
        {
            string[] statusJsons = GetJsonTestCases("statusJson").ToArray();
            Assert.Equal(ModuleStatus.Stopped, ModuleSerde.Instance.Deserialize<DockerModule>(statusJsons[0]).DesiredStatus);
            Assert.Equal(ModuleStatus.Unknown, ModuleSerde.Instance.Deserialize<DockerModule>(statusJsons[1]).DesiredStatus);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetExceptionJsonInputs))]
        public void TestDeserializeExceptionJson(string inputJson)
        {
            /*
             * Removed from json inputs because "settings" is no longer required to have "image" and "createOptions":
             * {
             *   ""Version"":""<semantic_version_number>"",
             *   ""Type"":""docker"",
             *   ""Status"":""running"",
             *   ""Settings"":{},
             *   ""Configuration"": {
             *   ""id"": ""1""
             *   }
             * }
             */
            Assert.Throws<JsonSerializationException>(() => ModuleSerde.Instance.Deserialize<DockerModule>(inputJson));
        }

        [Fact]
        [Unit]
        public void TestSerialize()
        {
            string jsonFromDockerModule = ModuleSerde.Instance.Serialize(Module8);
            IModule myModule = ModuleSerde.Instance.Deserialize<DockerModule>(jsonFromDockerModule);
            IModule moduleFromSerializedModule = ModuleSerde.Instance.Deserialize<DockerModule>(SerializedModule);

            myModule.Name = "mod1";
            moduleFromSerializedModule.Name = "mod1";

            Assert.True(Module8.Equals(myModule));
            Assert.True(moduleFromSerializedModule.Equals(Module8));
            Assert.True(moduleFromSerializedModule.Equals(Module1));
        }

        [Fact]
        [Unit]
        public void TestSerializeContainerCreateConfig()
        {
            // Arrange
            string createOptions = @"{""Env"": [""k1=v1"",""k2=v2""]}";
            var config = new DockerConfig("ubuntu:latest", createOptions);
            var module = new DockerModule("testser", "1.0", ModuleStatus.Running, RestartPolicy.OnUnhealthy, config, ImagePullPolicy.OnCreate, null, null);

            // Act
            string json = ModuleSerde.Instance.Serialize(module);

            // Assert
            var mod2 = ModuleSerde.Instance.Deserialize<DockerModule>(json);
            Assert.Contains(@"""k1=v1""", JsonConvert.SerializeObject(mod2.Config.CreateOptions));
            Assert.Contains(@"""k2=v2""", JsonConvert.SerializeObject(mod2.Config.CreateOptions));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidEnvJsonInputs))]
        public void TestDeserializeInvalidEnvJson(string inputJson)
        {
            Assert.Throws<JsonSerializationException>(
                () => ModuleSerde.Instance.Deserialize<DockerModule>(inputJson));
        }

        static IEnumerable<string> GetJsonTestCases(string subset)
        {
            var val = (JArray)TestJsonInputs.GetValue(subset);
            return val.Children().Select(token => token.ToString());
        }
    }
}
