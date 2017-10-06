// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;
    using Newtonsoft.Json.Linq;
    using System.Linq;

    [ExcludeFromCodeCoverage]
    public class DockerModuleTest
    {
        static readonly DockerConfig Config1 = new DockerConfig("image1", "42", @"{""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}], ""42/tcp"": [{""HostPort"": ""42""}]}}}");
        static readonly DockerConfig Config2 = new DockerConfig("image2", "42", @"{""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}], ""42/tcp"": [{""HostPort"": ""42""}]}}}");

        static readonly IModule Module1 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1);
        static readonly IModule Module2 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1);
        static readonly IModule Module3 = new DockerModule("mod3", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1);
        static readonly IModule Module4 = new DockerModule("mod1", "version2", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1);
        static readonly IModule Module6 = new DockerModule("mod1", "version1", ModuleStatus.Unknown, RestartPolicy.OnUnhealthy, Config1);
        static readonly IModule Module7 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config2);
        static readonly DockerModule Module8 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1);
        static readonly IModule Module9 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.Always, Config1);

        static readonly DockerModule ValidJsonModule = new DockerModule("<module_name>", "<semantic_version_number>", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1);

        const string SerializedModule = @"{""name"":""mod1"",""version"":""version1"",""type"":""docker"",""status"":""running"",""restartPolicy"":""on-unhealthy"",""config"":{""image"":""image1"",""tag"":""42"", ""createOptions"": {""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}], ""42/tcp"": [{""HostPort"": ""42""}]}}}}}";

        static readonly JObject TestJsonInputs = JsonConvert.DeserializeObject<JObject>(@"
{
   ""invalidEnvJson"":[
      {
         ""name"":""mod1"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
            ""createoptions"":{
               ""env"":10
            }
         }
      },
      {
         ""name"":""mod2"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
            ""createoptions"":{
               ""env"":""boo""
            }
         }
      },
      {
         ""name"":""mod2"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
            ""createoptions"":{
               ""env"":true
            }
         }
      }
   ],
   ""validEnvJson"":[
      {
         ""name"":""mod1"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-failure"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
            ""createoptions"":{
               ""env"":[
                  ""k1=v1"",
                  ""k2=v2""
               ]
            }
         }
      },
      {
         ""name"":""mod2"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""always"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
            ""createoptions"":{
               ""env"":[""""]
            }
         }
      },
      {
         ""name"":""mod3"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
            ""createoptions"":{
               ""env"":[]
            }
         }
      },
      {
         ""name"":""mod4"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""never"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1""
         }
      }
   ],
   ""validJson"":[
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"": ""on-unhealthy"",
         ""Config"":{
            ""Image"":""image1"",
            ""Tag"":""42"",
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
         }
      },
      {
         ""name"":""<module_name>"",
         ""version"":""<semantic_version_number>"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""42"",
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
         }
      },
      {
         ""NAME"":""<module_name>"",
         ""VERSION"":""<semantic_version_number>"",
         ""TYPE"":""docker"",
         ""STATUS"":""running"",
         ""RESTARTPOLICY"": ""on-unhealthy"",
         ""CONFIG"":{
            ""IMAGE"":""image1"",
            ""TAG"":""42"",
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
         }
      }
   ],
   ""throwsException"":[
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Config"":{
            ""Image"":""<docker_image_name>""
         }
      },
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""Config"":{
            ""Image"":""<docker_image_name>""
         }
      },
      {
         ""Name"":""<module_name>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""Config"":{
            ""Image"":""<docker_image_name>""
         }
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Status"":""running"",
         ""Config"":{
            ""Image"":""<docker_image_name>""
         }
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running""
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""Config"":{

         }
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""Config"":{

         }
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"": ""no-such-policy"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
            ""portbindings"":{
               ""43/udp"":{
                  ""from"":""43"",
                  ""to"":""43"",
                  ""type"":""udp""
               },
               ""42/tcp"":{
                  ""from"":""42"",
                  ""to"":""42"",
                  ""type"":""tcp""
               }
            }
         }
      }
   ],
   ""statusJson"":[
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""stopped"",
         ""Config"":{
            ""Image"":""<docker_image_name>"",
            ""TAG"":""42""
         }
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""Unknown"",
         ""Config"":{
            ""Image"":""<docker_image_name>"",
            ""TAG"":""42""
         }
      }
   ]
}
");

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new DockerModule(null, "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1));
            Assert.Throws<ArgumentNullException>(() => new DockerModule("mod1", null, ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1));
            Assert.Throws<ArgumentNullException>(() => new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerModule("mod1", "version1", (ModuleStatus)int.MaxValue, RestartPolicy.OnUnhealthy, Config1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerModule("mod1", "version1", ModuleStatus.Running, (RestartPolicy)int.MaxValue, Config1));
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
            Assert.NotEqual(Module1, Module4);
            Assert.NotEqual(Module1, Module6);
            Assert.NotEqual(Module1, Module7);
            Assert.Equal(Module1, Module8);

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
        }

        static IEnumerable<string> GetJsonTestCases(string subset)
        {
            JArray val = (JArray)TestJsonInputs.GetValue(subset);
            return val.Children().Select(token => token.ToString());
        }

        static IEnumerable<object[]> GetValidJsonInputs()
        {
            return GetJsonTestCases("validJson").Select(s => new object[] { s });
        }

        static IEnumerable<object[]> GetExceptionJsonInputs()
        {
            return GetJsonTestCases("throwsException").Select(s => new object[] { s });
        }

        static IEnumerable<object[]> GetValidEnvJsonInputs()
        {
            return GetJsonTestCases("validEnvJson").Select(s => new object[] { s });
        }

        static IEnumerable<object[]> GetInvalidEnvJsonInputs()
        {
            return GetJsonTestCases("invalidEnvJson").Select(s => new object[] { s });
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetValidJsonInputs))]
        public void TestDeserializeValidJson(string inputJson)
        {
            DockerModule module = ModuleSerde.Instance.Deserialize<DockerModule>(inputJson);
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
            Assert.Throws<JsonSerializationException>(() => ModuleSerde.Instance.Deserialize<DockerModule>(inputJson));
        }

        [Fact]
        [Unit]
        public void TestSerialize()
        {
            string jsonFromDockerModule = ModuleSerde.Instance.Serialize(Module8);
            IModule myModule = ModuleSerde.Instance.Deserialize<DockerModule>(jsonFromDockerModule);
            IModule moduleFromSerializedModule = ModuleSerde.Instance.Deserialize<DockerModule>(SerializedModule);

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
            var config = new DockerConfig("ubuntu", "latest", createOptions);
            var module = new DockerModule("testser", "1.0", ModuleStatus.Running, RestartPolicy.OnUnhealthy, config);

            // Act
            string json = ModuleSerde.Instance.Serialize(module);

            // Assert
            DockerModule mod2 = ModuleSerde.Instance.Deserialize<DockerModule>(json);
            Assert.True(JsonConvert.SerializeObject(mod2.Config.CreateOptions).Contains(@"""k1=v1"""));
            Assert.True(JsonConvert.SerializeObject(mod2.Config.CreateOptions).Contains(@"""k2=v2"""));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidEnvJsonInputs))]
        public void TestDeserializeInvalidEnvJson(string inputJson)
        {
            Assert.Throws<JsonSerializationException>(
                () => ModuleSerde.Instance.Deserialize<DockerModule>(inputJson));
        }
    }
}
