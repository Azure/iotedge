// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class TestModuleTest
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();

        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly TestConfig Config2 = new TestConfig("image2");
        static readonly TestConfig Config3 = new TestConfig("image1");

        static readonly IModule Module1 = new TestModule("mod1", "version1", "type1", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module2 = new TestModule("mod1", "version1", "type1", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module3 = new TestModule("mod3", "version1", "type1", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module4 = new TestModule("mod1", "version2", "type1", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module5 = new TestModule("mod1", "version1", "type2", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module6 = new TestModule("mod1", "version1", "type1", ModuleStatus.Unknown, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module7 = new TestModule("mod1", "version1", "type1", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly TestModule Module8 = new TestModule("mod1", "version1", "type1", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);

        static readonly IModule ValidJsonModule = new TestModule("<module_name>", "<semantic_version_number>", "docker", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly string serializedModule = "{\"version\":\"version1\",\"type\":\"type1\",\"status\":\"running\",\"settings\":{\"image\":\"image1\"},\"restartPolicy\":\"on-unhealthy\",\"imagePullPolicy\":\"on-create\",\"configuration\":{\"id\":\"1\"}}";

        static readonly JObject TestJsonInputs = JsonConvert.DeserializeObject<JObject>(
            @"
{
  ""validJson"": [
    {
      ""Version"": ""<semantic_version_number>"",
      ""Type"": ""docker"",
      ""Status"": ""running"",
      ""RestartPolicy"": ""on-unhealthy"",
      ""ImagePullPolicy"": ""on-create"",
      ""Settings"": {
        ""Image"": ""image1""
      },
      ""Configuration"": {
        ""id"":""1""
      }
    },
    {
      ""version"": ""<semantic_version_number>"",
      ""type"": ""docker"",
      ""status"": ""running"",
      ""restartpolicy"": ""on-unhealthy"",
      ""imagepullpolicy"": ""on-create"",
      ""settings"": {
        ""image"": ""image1""
      },
      ""configuration"": {
        ""id"":""1""
      }
    },
    {
      ""VERSION"": ""<semantic_version_number>"",
      ""TYPE"": ""docker"",
      ""STATUS"": ""RUNNING"",
      ""RESTARTPOLICY"": ""on-unhealthy"",
      ""IMAGEPULLPOLICY"": ""on-create"",
      ""SETTINGS"": {
        ""IMAGE"": ""image1""
      },
      ""CONFIGURATION"": {
        ""id"":""1""
      }
    }
  ],
  ""statusJson"": [
    {
      ""Version"": ""<semantic_version_number>"",
      ""Type"": ""docker"",
      ""Status"": ""stopped"",
      ""RestartPolicy"": ""on-unhealthy"",
      ""ImagePullPolicy"": ""on-create"",
      ""Settings"": {
        ""Image"": ""<docker_image_name>""
      },
      ""Configuration"": {
        ""id"":""1""
      }
    },
    {
      ""Version"": ""<semantic_version_number>"",
      ""Type"": ""docker"",
      ""Status"": ""Unknown"",
      ""RestartPolicy"": ""on-unhealthy"",
      ""ImagePullPolicy"": ""on-create"",
      ""Settings"": {
        ""Image"": ""<docker_image_name>""
      },
      ""Configuration"": {
        ""id"":""1""
      }
    }
  ],
  ""throwsException"": [
    {
      ""Version"": ""<semantic_version_number>"",
      ""Type"": ""docker"",
      ""RestartPolicy"": ""on-unhealthy"",
      ""ImagePullPolicy"": ""on-create"",
      ""Settings"": {
        ""Image"": ""<docker_image_name>""
      },
      ""Configuration"": {
        ""id"":""1""
      }
    },
    {
      ""Type"": ""docker"",
      ""Status"": ""running"",
      ""RestartPolicy"": ""on-unhealthy"",
      ""ImagePullPolicy"": ""on-create"",
      ""Settings"": {
        ""Image"": ""<docker_image_name>""
      },
      ""Configuration"": {
        ""id"":""1""
      }
    },
    {
      ""Version"": ""<semantic_version_number>"",
      ""Status"": ""running"",
      ""RestartPolicy"": ""on-unhealthy"",
      ""ImagePullPolicy"": ""on-create"",
      ""Settings"": {
        ""Image"": ""<docker_image_name>""
      },
      ""Configuration"": {
        ""id"":""1""
      }
    },
    {
      ""Version"": ""<semantic_version_number>"",
      ""Type"": ""docker"",
      ""Settings"": ""running"",
      ""RestartPolicy"": ""on-unhealthy"",
      ""ImagePullPolicy"": ""on-create"",
      ""Configuration"": {
        ""id"":""1""
      }
    },
    {
      ""Version"": ""<semantic_version_number>"",
      ""Type"": ""docker"",
      ""Status"": ""running"",
      ""RestartPolicy"": ""on-unhealthy"",
      ""ImagePullPolicy"": ""on-create"",
      ""Settings"": {},
      ""Configuration"": {
        ""id"":""1""
      }
    },
    {
      ""Version"": ""<semantic_version_number>"",
      ""Type"": ""docker"",
      ""Status"": ""<bad_status>"",
      ""RestartPolicy"": ""on-unhealthy"",
      ""ImagePullPolicy"": ""never"",
      ""Settings"": {
        ""Image"": ""<docker_image_name>""
      },
      ""Configuration"": {
        ""id"":""1""
      }
    },
    {
      ""Version"": ""<semantic_version_number>"",
      ""Type"": ""docker"",
      ""Status"": ""<bad_status>"",
      ""RestartPolicy"": ""on-unhealthy"",
      ""ImagePullPolicy"": ""never"",
      ""Settings"": {
        ""Image"": ""<docker_image_name>""
      }
    },
    {
      ""Version"": ""<semantic_version_number>"",
      ""Type"": ""docker"",
      ""Status"": ""<bad_status>"",
      ""RestartPolicy"": ""on-unhealthy"",
      ""ImagePullPolicy"": ""<bad_image_pull_policy>"",
      ""Settings"": {
        ""Image"": ""<docker_image_name>""
      }
    }
  ]
}");

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
            Assert.Throws<ArgumentNullException>(() => new TestModule("mod1", null, "type1", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars));
            Assert.Throws<ArgumentNullException>(() => new TestModule("mod1", "version1", null, ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars));
            Assert.Throws<ArgumentNullException>(() => new TestModule("mod1", "version1", "type1", ModuleStatus.Running, null, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars));
        }

        [Fact]
        [Unit]
        public void TestEquality()
        {
            Assert.Equal(Module1, Module1);
            Assert.Equal(Module1, Module2);
            Assert.Equal(Module8, Module8);
            Assert.NotEqual(Module1, Module3);
            Assert.NotEqual(Module1, Module4);
            Assert.NotEqual(Module1, Module5);
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
            Assert.False(Module1.Equals((object)Module3));
            Assert.False(Module1.Equals(new object()));

            Assert.Equal(Module1.GetHashCode(), Module2.GetHashCode());
            Assert.NotEqual(Module1.GetHashCode(), Module3.GetHashCode());

            Assert.Equal(Config1, Config1);
            Assert.Equal(Config1, Config3);
            Assert.NotEqual(Config1, Config2);
            Assert.True(Config1.Equals((object)Config1));
            Assert.False(Config1.Equals(null));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetValidJsonInputs))]
        public void TestDeserializeValidJson(string inputJson)
        {
            var module = ModuleSerde.Instance.Deserialize<TestModule>(inputJson);
            module.Name = "<module_name>";
            Assert.True(ValidJsonModule.Equals(module));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetExceptionJsonInputs))]
        public void TestDeserializeExceptionJson(string inputJson)
        {
            Assert.Throws<JsonSerializationException>(() => ModuleSerde.Instance.Deserialize<TestModule>(inputJson));
        }

        [Fact]
        [Unit]
        public void TestDeserializeStatusJson()
        {
            string[] statusJsons = GetJsonTestCases("statusJson").ToArray();
            Assert.Equal(ModuleStatus.Stopped, ModuleSerde.Instance.Deserialize<TestModule>(statusJsons[0]).DesiredStatus);
            Assert.Equal(ModuleStatus.Unknown, ModuleSerde.Instance.Deserialize<TestModule>(statusJsons[1]).DesiredStatus);
        }

        [Fact]
        [Unit]
        public void ModuleDeserializeMustSpecifyClass()
        {
            string validJson = "{\"Name\":\"<module_name>\",\"Version\":\"<semantic_version_number>\",\"Type\":\"docker\",\"Status\":\"running\",\"Config\":{\"Image\":\"image1\"}}";

            Assert.ThrowsAny<JsonException>(() => ModuleSerde.Instance.Deserialize(validJson));
        }

        [Fact]
        [Unit]
        public void TestSerialize()
        {
            string jsonFromTestModule = ModuleSerde.Instance.Serialize(Module8);
            var myModule = ModuleSerde.Instance.Deserialize<TestModule>(jsonFromTestModule);
            IModule moduleFromSerializedModule = ModuleSerde.Instance.Deserialize<TestModule>(serializedModule);

            myModule.Name = "mod1";
            moduleFromSerializedModule.Name = "mod1";

            Assert.True(Module8.Equals(myModule));
            Assert.True(moduleFromSerializedModule.Equals(Module8));
            Assert.True(moduleFromSerializedModule.Equals(Module1));
        }

        static IEnumerable<string> GetJsonTestCases(string subset)
        {
            var val = (JArray)TestJsonInputs.GetValue(subset);
            return val.Children().Select(token => token.ToString());
        }
    }
}
