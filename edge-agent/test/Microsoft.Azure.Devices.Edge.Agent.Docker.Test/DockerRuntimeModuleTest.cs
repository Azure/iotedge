// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;
    using Newtonsoft.Json.Linq;

    [ExcludeFromCodeCoverage]
    [Unit]
    public class DockerRuntimeModuleTest
    {
        static readonly DockerConfig Config1 = new DockerConfig("image1", "42", @"{""HostConfig"": {""PortBinding"": {""42/tcp"": [{""HostPort"": ""42""}], ""43/udp"": [{""HostPort"": ""43""}]}}}");
        static readonly DockerConfig Config2 = new DockerConfig("image2", "42", @"{""HostConfig"": {""PortBinding"": {""42/tcp"": [{""HostPort"": ""42""}], ""43/udp"": [{""HostPort"": ""43""}]}}}");

        static readonly IModule Module1 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, null, DateTime.MinValue, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly IModule Module1a = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, null, DateTime.MinValue, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly IModule Module2 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly IModule Module2a = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly IModule Module3 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config2, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly IModule Module4 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, -1, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly IModule Module5 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 35 minutes", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly IModule Module6 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-07-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly IModule Module7 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.Parse("2017-08-05T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly IModule Module8 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly DockerModule Module9 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1);
        static readonly IModule Module10 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnFailure, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly IModule Module11 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 1, DateTime.MinValue, ModuleStatus.Running);
        static readonly IModule Module12 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Stopped);

        static readonly DockerConfig ValidConfig = new DockerConfig("image1", "42");
        static readonly DockerRuntimeModule ValidJsonModule = new DockerRuntimeModule("<module_name>", "<semantic_version_number>", ModuleStatus.Running, RestartPolicy.OnFailure, ValidConfig, 0, "<status description>", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.Parse("2017-08-05T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), 1, DateTime.Parse("2017-08-06T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), ModuleStatus.Running);

        const string SerializedModule1 = @"{""name"":""mod1"",""version"":""version1"",""type"":""docker"",""status"":""running"",""restartPolicy"":""on-unhealthy"",""exitcode"":0,""restartcount"":0,""lastrestarttimeutc"":""0001-01-01T00:00:00Z"",""runtimestatus"":""running"",""config"":{""image"":""image1"",""tag"":""42"",""createOptions"":{""HostConfig"":{""PortBinding"":{""42/tcp"":[{""HostPort"":""42""}],""43/udp"":[{""HostPort"":""43""}]}}}}}";
        const string SerializedModule2 = @"{""name"":""mod1"",""version"":""version1"",""type"":""docker"",""status"":""running"",""restartPolicy"":""on-unhealthy"",""exitcode"":0,""statusdescription"":""Running 1 minute"",""laststarttimeutc"":""2017-08-04T17:52:13.0419502Z"",""lastexittimeutc"":""0001-01-01T00:00:00Z"",""restartcount"":0,""lastrestarttimeutc"":""0001-01-01T00:00:00Z"",""runtimestatus"":""running"",""config"":{""image"":""image1"",""tag"":""42"",""createOptions"":{""HostConfig"":{""PortBinding"":{""42/tcp"":[{""HostPort"":""42""}],""43/udp"":[{""HostPort"":""43""}]}}}}}";

        static readonly JObject TestJsonInputs = JsonConvert.DeserializeObject<JObject>(@"
{
   ""invalidExitCodeJson"":[
      {
         ""name"": ""mod1"",
         ""version"": ""1.0.0"",
         ""type"": ""docker"",
         ""status"": ""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""config"": {
            ""image"": ""image1"",
            ""tag"": ""ver1"",
         },
         ""exitcode"": ""zero"",
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      },
      {
         ""name"":""mod2"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
         },
         ""exitcode"": true,
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
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
         },
         ""exitcode"": [ 0 ,1 ],
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      },
      {
         ""name"":""mod4"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1"",
         },
         ""exitcode"": {},
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      }
   ],
   ""invalidStatusDescription"":[
      {
         ""name"":""mod3"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1""
         },
         ""exitcode"": 0,
         ""statusdescription"" : {},
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      },
      {
         ""name"":""mod4"",
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""ver1""
         },
         ""exitcode"": 0,
         ""statusdescription"" : [],
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
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
         ""laststarttimeutc"" : {},
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
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
         ""laststarttimeutc"" : [],
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      }
   ],
   ""invalidLastRestartTime"":[
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
         ""lastrestarttimeutc"" : {},
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
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
         ""lastrestarttimeutc"" : [],
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      }
   ],
   ""invalidRestartCount"":[
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""2017-08-04T17:52:13.0419502Z"",
         ""LastExitTimeUtc"" : ""2017-08-05T17:52:13.0419502Z"",
         ""RestartCount"" : -1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""running""
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""2017-08-04T17:52:13.0419502Z"",
         ""LastExitTimeUtc"" : ""2017-08-05T17:52:13.0419502Z"",
         ""RestartCount"" : {},
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""running""
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""2017-08-04T17:52:13.0419502Z"",
         ""LastExitTimeUtc"" : ""2017-08-05T17:52:13.0419502Z"",
         ""RestartCount"" : [],
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""running""
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""2017-08-04T17:52:13.0419502Z"",
         ""LastExitTimeUtc"" : ""2017-08-05T17:52:13.0419502Z"",
         ""RestartCount"" : ""boo"",
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""running""
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""2017-08-04T17:52:13.0419502Z"",
         ""LastExitTimeUtc"" : ""2017-08-05T17:52:13.0419502Z"",
         ""RestartCount"" : null,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""running""
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
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : {}
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
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : []
      }
   ],
   ""validStatus"":[
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""unknown"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""unknown""
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""stopped"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""stopped""
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""backoff"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""backoff""
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""unhealthy"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""unhealthy""
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""running""
      },
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""failed"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""failed""
      }
   ],
   ""validJson"":[
      {
         ""Name"":""<module_name>"",
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""Config"":{
            ""Image"":""image1"",
            ""tag"":""42"",
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""2017-08-04T17:52:13.0419502Z"",
         ""LastExitTimeUtc"" : ""2017-08-05T17:52:13.0419502Z"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""running""
      },
      {
         ""name"":""<module_name>"",
         ""version"":""<semantic_version_number>"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartpolicy"":""on-failure"",
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""2017-08-04T17:52:13.0419502Z"",
         ""lastexittimeutc"" : ""2017-08-05T17:52:13.0419502Z"",
         ""config"":{
            ""image"":""image1"",
            ""tag"":""42"",
         },
         ""restartcount"" : 1,
         ""lastrestarttimeutc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""runtimestatus"":""running""
      },
      {
         ""EXITCODE"": 0,
         ""STATUSDESCRIPTION"" : ""<status description>"",
         ""LASTSTARTTIMEUTC"" : ""2017-08-04T17:52:13.0419502Z"",
         ""LASTEXITTIMEUTC"" : ""2017-08-05T17:52:13.0419502Z"",
         ""NAME"":""<module_name>"",
         ""VERSION"":""<semantic_version_number>"",
         ""TYPE"":""docker"",
         ""STATUS"":""RUNNING"",
         ""RESTARTPOLICY"":""on-failure"",
         ""CONFIG"":{
            ""IMAGE"":""image1"",
            ""TAG"":""42"",
         },
         ""RESTARTCOUNT"" : 1,
         ""LASTRESTARTTIMEUTC"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RUNTIMESTATUS"":""running""
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

        static IEnumerable<object[]> GetValidStatusInputs()
        {
            return GetJsonTestCases("validStatus").Select(s => new object[] { s });
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

        static IEnumerable<object[]> GetInvalidRestartCounts()
        {
            return GetJsonTestCases("invalidRestartCount").Select(s => new object[] { s });
        }

        static IEnumerable<object[]> GetInvalidLastRestartTimes()
        {
            return GetJsonTestCases("invalidLastRestartTime").Select(s => new object[] { s });
        }

        [Fact]
        public void TestConstructor()
        {
            DateTime lastStartTime = DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind);

            // null module name
            Assert.Throws<ArgumentNullException>(() => new DockerRuntimeModule(null, "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running));

            // null version
            Assert.Throws<ArgumentNullException>(() => new DockerRuntimeModule("mod1", null, ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running));

            // null docker config
            Assert.Throws<ArgumentNullException>(() => new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, null, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running));

            // bad desired status
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerRuntimeModule("mod1", "version1", (ModuleStatus)int.MaxValue, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running));

            // bad restart policy
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, (RestartPolicy)int.MaxValue, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running));

            // bad runtime status
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, (ModuleStatus)int.MaxValue));

            // bad restart count
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, -1, DateTime.MinValue, ModuleStatus.Running));

            var env1 = new DockerRuntimeModule("name", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, null, lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
            var env2 = new DockerRuntimeModule("name", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.MinValue, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
            var env3 = new DockerRuntimeModule("name", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
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
            Assert.NotEqual(Module8, Module10);
            Assert.NotEqual(Module8, Module11);
            Assert.NotEqual(Module8, Module12);

            Assert.False(Module1.Equals(null));
            Assert.False(Module2.Equals(null));

            Assert.False(Module1.Equals(new object()));
            Assert.False(Module2.Equals(new object()));
        }

        [Fact]
        public void TestSerialize()
        {
            string jsonFromDockerModule = ModuleSerde.Instance.Serialize(Module2);
            IModule myModule = ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(jsonFromDockerModule);
            IModule moduleFromSerializedModule = ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(SerializedModule2);

            Assert.True(Module2.Equals(myModule));
            Assert.True(moduleFromSerializedModule.Equals(Module2));
        }

        [Fact]
        public void TestSerializeWithNull()
        {
            string jsonFromDockerModule = ModuleSerde.Instance.Serialize(Module1);
            IModule myModule = ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(jsonFromDockerModule);
            IModule moduleFromSerializedModule = ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(SerializedModule1);

            Assert.True(Module1.Equals(myModule));
            Assert.True(moduleFromSerializedModule.Equals(Module1));
        }

        [Theory]
        [MemberData(nameof(GetValidJsonInputs))]
        public void TestDeserializeValidJson(string inputJson)
        {
            DockerRuntimeModule module = ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(inputJson);
            Assert.True(ValidJsonModule.Equals(module));
        }

        [Theory]
        [MemberData(nameof(GetValidStatusInputs))]
        public void TestDeserializeValidStatus(string inputJson)
        {
            DockerRuntimeModule module = ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(inputJson);
            Assert.NotNull(module);
        }

        [Theory]
        [MemberData(nameof(GetInvalidExitCodes))]
        public void TestDeserializeExitCode(string inputJson)
        {
            Assert.ThrowsAny<JsonException>(() => ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(inputJson));
        }

        [Theory]
        [MemberData(nameof(GetInvalidStatusDescription))]
        public void TestDeserializeStatusDescription(string inputJson)
        {
            Assert.ThrowsAny<JsonException>(() => ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(inputJson));
        }

        [Theory]
        [MemberData(nameof(GetInvalidLastStartTimes))]
        public void TestDeserializeLastStartTimes(string inputJson)
        {
            Assert.ThrowsAny<JsonException>(() => ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(inputJson));
        }

        [Theory]
        [MemberData(nameof(GetInvalidLastExitTimes))]
        public void TestDeserializeLastExitTime(string inputJson)
        {
            Assert.ThrowsAny<JsonException>(() => ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(inputJson));
        }

        [Theory]
        [MemberData(nameof(GetInvalidRestartCounts))]
        public void TestDeserializeRestartCounts(string inputJson)
        {
            Assert.ThrowsAny<JsonException>(() => ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(inputJson));
        }

        [Theory]
        [MemberData(nameof(GetInvalidLastRestartTimes))]
        public void TestDeserializeLastRestartTimes(string inputJson)
        {
            Assert.ThrowsAny<JsonException>(() => ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(inputJson));
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
