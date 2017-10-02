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
    [Unit]
    public class DockerEnvModuleTest
    {
        static readonly DockerConfig Config1 = new DockerConfig("image1", "42", @"{""HostConfig"": {""PortBinding"": {""42/tcp"": [{""HostPort"": ""42""}], ""43/udp"": [{""HostPort"": ""43""}]}}}");
        static readonly DockerConfig Config2 = new DockerConfig("image2", "42", @"{""HostConfig"": {""PortBinding"": {""42/tcp"": [{""HostPort"": ""42""}], ""43/udp"": [{""HostPort"": ""43""}]}}}");

        static readonly IModule Module1 = new DockerEnvModule("mod1", "version1", ModuleStatus.Running, Config1, 0, null, null, null);
        static readonly IModule Module1a = new DockerEnvModule("mod1", "version1", ModuleStatus.Running, Config1, 0, null, null, null);
        static readonly IModule Module2 = new DockerEnvModule("mod1", "version1", ModuleStatus.Running, Config1, 0, "Running 1 minute", "2017-08-04T17:52:13.0419502Z", "0001-01-01T00:00:00Z");
        static readonly IModule Module2a = new DockerEnvModule("mod1", "version1", ModuleStatus.Running, Config1, 0, "Running 1 minute", "2017-08-04T17:52:13.0419502Z", "0001-01-01T00:00:00Z");
        static readonly IModule Module3 = new DockerEnvModule("mod1", "version1", ModuleStatus.Running, Config2, 0, "Running 1 minute", "2017-08-04T17:52:13.0419502Z", "0001-01-01T00:00:00Z");
        static readonly IModule Module4 = new DockerEnvModule("mod1", "version1", ModuleStatus.Running, Config1, -1, "Running 1 minute", "2017-08-04T17:52:13.0419502Z", "0001-01-01T00:00:00Z");
        static readonly IModule Module5 = new DockerEnvModule("mod1", "version1", ModuleStatus.Running, Config1, 0, "Running 35 minutes", "2017-08-04T17:52:13.0419502Z", "0001-01-01T00:00:00Z");
        static readonly IModule Module6 = new DockerEnvModule("mod1", "version1", ModuleStatus.Running, Config1, 0, "Running 1 minute", "different start time", "0001-01-01T00:00:00Z");
        static readonly IModule Module7 = new DockerEnvModule("mod1", "version1", ModuleStatus.Running, Config1, 0, "Running 1 minute", "2017-08-04T17:52:13.0419502Z", "different stop time");
        static readonly IModule Module8 = new DockerEnvModule("mod1", "version1", ModuleStatus.Running, Config1, 0, "Running 1 minute", "2017-08-04T17:52:13.0419502Z", "0001-01-01T00:00:00Z");
        static readonly DockerModule Module9 = new DockerModule("mod1", "version1", ModuleStatus.Running, Config1);

        static readonly DockerConfig ValidConfig = new DockerConfig("image1", "42");
        static readonly DockerEnvModule ValidJsonModule = new DockerEnvModule("<module_name>", "<semantic_version_number>", ModuleStatus.Running, ValidConfig, 0, "<status description>", "<last start time>", "<last exit time>");

        const string SerializedModule1 = @"{""name"":""mod1"",""version"":""version1"",""type"":""docker"",""status"":""running"",""exitcode"":0,""config"":{""image"":""image1"",""tag"":""42"",""createOptions"":{""HostConfig"":{""PortBinding"":{""42/tcp"":[{""HostPort"":""42""}],""43/udp"":[{""HostPort"":""43""}]}}}}}";
        const string SerializedModule2 = @"{""name"":""mod1"",""version"":""version1"",""type"":""docker"",""status"":""running"",""exitcode"":0,""statusdescription"":""Running 1 minute"",""laststarttime"":""2017-08-04T17:52:13.0419502Z"",""lastexittime"":""0001-01-01T00:00:00Z"",""config"":{""image"":""image1"",""tag"":""42"",""createOptions"":{""HostConfig"":{""PortBinding"":{""42/tcp"":[{""HostPort"":""42""}],""43/udp"":[{""HostPort"":""43""}]}}}}}";

        static readonly JObject TestJsonInputs = JsonConvert.DeserializeObject<JObject>(@"
{
   ""invalidExitCodeJson"":[
      {
         ""name"":""mod1"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
         },
         ""exitcode"": ""zero"",
         ""statusdescription"" : ""<status description>"",
         ""laststarttime"" : ""<last start time>"",
         ""lastexittime"" : ""<last exit time>""
      },
      {
         ""name"":""mod2"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
         },
         ""exitcode"": true,
         ""statusdescription"" : ""<status description>"",
         ""laststarttime"" : ""<last start time>"",
         ""lastexittime"" : ""<last exit time>""
      },
      {
         ""name"":""mod3"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
         },
         ""exitcode"": [ 0 ,1 ],
         ""statusdescription"" : ""<status description>"",
         ""laststarttime"" : ""<last start time>"",
         ""lastexittime"" : ""<last exit time>""
      },
      {
         ""name"":""mod4"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
         },
         ""exitcode"": {},
         ""statusdescription"" : ""<status description>"",
         ""laststarttime"" : ""<last start time>"",
         ""lastexittime"" : ""<last exit time>""
      }
   ],
   ""invalidStatusDescription"":[
      {
         ""name"":""mod3"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1""
         },
         ""exitcode"": 0,
         ""statusdescription"" : {},
         ""laststarttime"" : ""<last start time>"",
         ""lastexittime"" : ""<last exit time>""
      },
      {
         ""name"":""mod4"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1""
         },
         ""exitcode"": 0,
         ""statusdescription"" : [],
         ""laststarttime"" : ""<last start time>"",
         ""lastexittime"" : ""<last exit time>""
      }
   ],
   ""invalidLastStartTime"":[
      {
         ""name"":""mod3"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1""
         },
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""laststarttime"" : {},
         ""lastexittime"" : ""<last exit time>""
      },
      {
         ""name"":""mod4"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1""
         },
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""laststarttime"" : [],
         ""lastexittime"" : ""<last exit time>""
      }
   ],
   ""invalidLastExitTime"":[
      {
         ""name"":""mod3"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1""
         },
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""laststarttime"" : ""<last start time>"",
         ""lastexittime"" : {}
      },
      {
         ""name"":""mod4"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1""
         },
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""laststarttime"" : ""<last start time>"",
         ""lastexittime"" : []
      }
   ],
   ""validJson"":[
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTime"" : ""<last start time>"",
         ""LastExitTime"" : ""<last exit time>""
      },
      {
         ""name"":""<module_name>"",
         ""version"":""<semantic_version_number>"",
         ""type"":""docker"",
         ""status"":""running"",
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""laststarttime"" : ""<last start time>"",
         ""lastexittime"" : ""<last exit time>"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""42"",
         }
      },
      {
         ""EXITCODE"": 0,
         ""STATUSDESCRIPTION"" : ""<status description>"",
         ""LASTSTARTTIME"" : ""<last start time>"",
         ""LASTEXITTIME"" : ""<last exit time>"",
         ""NAME"":""<module_name>"",
         ""VERSION"":""<semantic_version_number>"",
         ""TYPE"":""docker"",
         ""STATUS"":""RUNNING"",
         ""CONFIG"":{
            ""IMAGE"":""image1"",
            ""TAG"":""42"",
         }
      }
   ],
}
");
        static IEnumerable<string> GetJsonTestCases(string subset)
        {
            JArray val = (JArray)TestJsonInputs.GetValue(subset);
            return val.Children().Select(token => token.ToString());
        }

        static IEnumerable<object[]> GetValidJsonInputs()
        {
            return GetJsonTestCases("validJson").Select(s => new object[] { s });
        }

        static IEnumerable<object[]> GetInvalidExitCodes()
        {
            return GetJsonTestCases("invalidExitCodeJson").Select(s => new object[] { s });
        }

        static IEnumerable<object[]> GetInvalidStatusDescription()
        {
            return GetJsonTestCases("invalidStatusDescription").Select(s => new object[] { s });
        }

        static IEnumerable<object[]> GetInvalidLastStartTimes()
        {
            return GetJsonTestCases("invalidLastStartTime").Select(s => new object[] { s });
        }

        static IEnumerable<object[]> GetInvalidLastExitTimes()
        {
            return GetJsonTestCases("invalidLastExitTime").Select(s => new object[] { s });
        }

        [Fact]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new DockerEnvModule(null, "version1", ModuleStatus.Running, Config1, 0, "Running 1 minute", "2017-08-04T17:52:13.0419502Z", "0001-01-01T00:00:00Z"));
            Assert.Throws<ArgumentNullException>(() => new DockerEnvModule("mod1", null, ModuleStatus.Running, Config1, 0, "Running 1 minute", "2017-08-04T17:52:13.0419502Z", "0001-01-01T00:00:00Z"));
            Assert.Throws<ArgumentNullException>(() => new DockerEnvModule("mod1", "version1", ModuleStatus.Running, null, 0, "Running 1 minute", "2017-08-04T17:52:13.0419502Z", "0001-01-01T00:00:00Z"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerEnvModule("mod1", "version1", (ModuleStatus)int.MaxValue, Config1, 0, "Running 1 minute", "2017-08-04T17:52:13.0419502Z", "0001-01-01T00:00:00Z"));
            var env1 = new DockerEnvModule("name", "version1", ModuleStatus.Running, Config1, 0, null, "2017-08-04T17:52:13.0419502Z", "0001-01-01T00:00:00Z");
            var env2 = new DockerEnvModule("name", "version1", ModuleStatus.Running, Config1, 0, "Running 1 minute", null, "0001-01-01T00:00:00Z");
            var env3 = new DockerEnvModule("name", "version1", ModuleStatus.Running, Config1, 0, "Running 1 minute", "2017-08-04T17:52:13.0419502Z", null);
            Assert.NotNull(env1);
            Assert.NotNull(env2);
            Assert.NotNull(env3);
        }

        [Fact]
        public void TestEquality()
        {
            Assert.Equal(Module2, Module2);
            Assert.Equal(Module2, Module8);
            Assert.Equal(Module2, Module9);
            Assert.Equal(Module9, Module2);
            Assert.Equal(Module1, Module9);
            Assert.Equal(Module9, Module1);

            Assert.NotEqual(Module1, Module2);
            Assert.NotEqual(Module2, Module3);
            Assert.NotEqual(Module2, Module4);
            Assert.NotEqual(Module2, Module5);
            Assert.NotEqual(Module2, Module6);
            Assert.NotEqual(Module2, Module7);

            Assert.False(Module1.Equals(null));
            Assert.False(Module2.Equals(null));

            Assert.False(Module1.Equals(new object()));
            Assert.False(Module2.Equals(new object()));
        }



        [Fact]
        public void TestSerialize()
        {
            string jsonFromDockerModule = ModuleSerde.Instance.Serialize(Module2);
            IModule myModule = ModuleSerde.Instance.Deserialize<DockerEnvModule>(jsonFromDockerModule);
            IModule moduleFromSerializedModule = ModuleSerde.Instance.Deserialize<DockerEnvModule>(SerializedModule2);

            Assert.True(Module2.Equals(myModule));
            Assert.True(moduleFromSerializedModule.Equals(Module2));
        }

        [Fact]
        public void TestSerializeWithNull()
        {
            string jsonFromDockerModule = ModuleSerde.Instance.Serialize(Module1);
            IModule myModule = ModuleSerde.Instance.Deserialize<DockerEnvModule>(jsonFromDockerModule);
            IModule moduleFromSerializedModule = ModuleSerde.Instance.Deserialize<DockerEnvModule>(SerializedModule1);

            Assert.True(Module1.Equals(myModule));
            Assert.True(moduleFromSerializedModule.Equals(Module1));
        }

        [Theory]
        [MemberData(nameof(GetValidJsonInputs))]
        public void TestDeserializeValidJson(string inputJson)
        {
            DockerEnvModule module = ModuleSerde.Instance.Deserialize<DockerEnvModule>(inputJson);
            Assert.True(ValidJsonModule.Equals(module));
        }

        [Theory]
        [MemberData(nameof(GetInvalidExitCodes))]
        public void TestDeserializeExitCode(string inputJson)
        {
            Assert.ThrowsAny<JsonException>(() => ModuleSerde.Instance.Deserialize<DockerEnvModule>(inputJson));
        }

        [Theory]
        [MemberData(nameof(GetInvalidStatusDescription))]
        public void TestDeserializeStatusDescription(string inputJson)
        {
            Assert.ThrowsAny<JsonException>(() => ModuleSerde.Instance.Deserialize<DockerEnvModule>(inputJson));
        }

        [Theory]
        [MemberData(nameof(GetInvalidLastStartTimes))]
        public void TestDeserializeLastStartTimes(string inputJson)
        {
            Assert.ThrowsAny<JsonException>(() => ModuleSerde.Instance.Deserialize<DockerEnvModule>(inputJson));
        }

        [Theory]
        [MemberData(nameof(GetInvalidLastExitTimes))]
        public void TestDeserializeLastExitTime(string inputJson)
        {
            Assert.ThrowsAny<JsonException>(() => ModuleSerde.Instance.Deserialize<DockerEnvModule>(inputJson));
        }

        [Fact]
        public void TestHashCode()
        {
            Assert.Equal(Module1.GetHashCode(), Module1a.GetHashCode());
            Assert.Equal(Module2.GetHashCode(), Module2a.GetHashCode());

            Assert.NotEqual(Module1.GetHashCode(), Module2.GetHashCode());
            Assert.NotEqual(Module1.GetHashCode(), Module8.GetHashCode());
            Assert.NotEqual(Module2.GetHashCode(), Module3.GetHashCode());
            Assert.NotEqual(Module2.GetHashCode(), Module4.GetHashCode());
            Assert.NotEqual(Module2.GetHashCode(), Module5.GetHashCode());
            Assert.NotEqual(Module2.GetHashCode(), Module6.GetHashCode());
            Assert.NotEqual(Module2.GetHashCode(), Module7.GetHashCode());
        }
    }
}
