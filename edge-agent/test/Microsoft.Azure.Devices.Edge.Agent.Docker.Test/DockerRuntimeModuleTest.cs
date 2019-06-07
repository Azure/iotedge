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
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [ExcludeFromCodeCoverage]
    [Unit]
    public class DockerRuntimeModuleTest
    {
        const string SerializedModule1 = @"{""version"":""version1"",""type"":""docker"",""status"":""running"",""restartPolicy"":""on-unhealthy"",""imagePullPolicy"":""on-create"",""exitcode"":0,""restartcount"":0,""lastrestarttimeutc"":""0001-01-01T00:00:00Z"",""runtimestatus"":""running"",""settings"":{""image"":""image1:42"",""createOptions"":{""HostConfig"":{""PortBinding"":{""42/tcp"":[{""HostPort"":""42""}],""43/udp"":[{""HostPort"":""43""}]}}}},""configuration"":{""id"":""1""}}";
        const string SerializedModule2 = @"{""version"":""version1"",""type"":""docker"",""status"":""running"",""restartPolicy"":""on-unhealthy"",""imagePullPolicy"":""on-create"",""exitcode"":0,""statusdescription"":""Running 1 minute"",""laststarttimeutc"":""2017-08-04T17:52:13.0419502Z"",""lastexittimeutc"":""0001-01-01T00:00:00Z"",""restartcount"":0,""lastrestarttimeutc"":""0001-01-01T00:00:00Z"",""runtimestatus"":""running"",""settings"":{""image"":""image1:42"",""createOptions"":{""HostConfig"":{""PortBinding"":{""42/tcp"":[{""HostPort"":""42""}],""43/udp"":[{""HostPort"":""43""}]}}}},""configuration"":{""id"":""1""}}";

        static readonly ConfigurationInfo DefaultConfigurationInfo = null;
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly DockerConfig Config1 = new DockerReportedConfig("image1:42", @"{""HostConfig"": {""PortBinding"": {""42/tcp"": [{""HostPort"": ""42""}], ""43/udp"": [{""HostPort"": ""43""}]}}}", "foo");
        static readonly DockerConfig Config2 = new DockerReportedConfig("image2:42", @"{""HostConfig"": {""PortBinding"": {""42/tcp"": [{""HostPort"": ""42""}], ""43/udp"": [{""HostPort"": ""43""}]}}}", "foo");

        static readonly IModule Module1 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, null, DateTime.MinValue, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module1A = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, null, DateTime.MinValue, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module2 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module2A = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module3 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config2, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module4 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, -1, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module5 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 35 minutes", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module6 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-07-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module7 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.Parse("2017-08-05T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module8 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly DockerModule Module9 = new DockerModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module10 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnFailure, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module11 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 1, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module12 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Stopped, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module13 = new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.Never, DefaultConfigurationInfo, EnvVars);

        static readonly DockerConfig ValidConfig = new DockerReportedConfig("image1:42", (string)null, "sha256:75");
        static readonly DockerRuntimeModule ValidJsonModule = new DockerRuntimeModule("<module_name>", "<semantic_version_number>", ModuleStatus.Running, RestartPolicy.OnFailure, ValidConfig, 0, "<status description>", DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), DateTime.Parse("2017-08-05T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), 1, DateTime.Parse("2017-08-06T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind), ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);

        static readonly JObject TestJsonInputs = JsonConvert.DeserializeObject<JObject>(
            @"
{
   ""invalidExitCodeJson"":[
      {
         ""version"": ""1.0.0"",
         ""type"": ""docker"",
         ""status"": ""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""imagePullPolicy"": ""on-create"",
         ""settings"": {
            ""image"": ""image1:ver1""
         },
         ""exitcode"": ""zero"",
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""imagePullPolicy"": ""on-create"",
         ""settings"":{
            ""image"":""image1:ver1""
         },
         ""exitcode"": true,
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""imagePullPolicy"": ""on-create"",
         ""settings"":{
            ""image"":""image1:ver1""
         },
         ""exitcode"": [ 0 ,1 ],
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""imagePullPolicy"": ""on-create"",
         ""settings"":{
            ""image"":""image1:ver1""
         },
         ""exitcode"": {},
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      }
   ],
   ""invalidStatusDescription"":[
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""imagePullPolicy"": ""on-create"",
         ""settings"":{
            ""image"":""image1:ver1""
         },
         ""exitcode"": 0,
         ""statusdescription"" : {},
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartPolicy"": ""on-unhealthy"",
         ""imagePullPolicy"": ""on-create"",
         ""settings"":{
            ""image"":""image1:ver1"",
         },
         ""exitcode"": 0,
         ""statusdescription"" : [],
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      }
   ],
   ""invalidLastStartTime"":[
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""settings"":{
            ""image"":""image1:ver1"",
         },
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : {},
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""settings"":{
            ""image"":""image1:ver1"",
         },
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : [],
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      }
   ],
   ""invalidLastRestartTime"":[
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""settings"":{
            ""image"":""image1:ver1"",
         },
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""lastrestarttimeutc"" : {},
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""settings"":{
            ""image"":""image1:ver1""
         },
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""lastrestarttimeutc"" : [],
         ""lastexittimeutc"" : ""0001-01-01T00:00:00.0000000""
      }
   ],
   ""invalidRestartCount"":[
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:ver1"",
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
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:42"",
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
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:42"",
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
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:42"",
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
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:ver1""
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
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""settings"":{
            ""image"":""image1:ver1""
         },
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : {}
      },
      {
         ""version"":""1.0.0"",
         ""type"":""docker"",
         ""status"":""running"",
         ""settings"":{
            ""image"":""image1:42""
         },
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""0001-01-01T00:00:00.0000000"",
         ""lastexittimeutc"" : []
      }
   ],
   ""validStatus"":[
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""unknown"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:42""
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""unknown"",
         ""Configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""stopped"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:42""
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""stopped"",
         ""Configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""backoff"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:42""
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""backoff"",
         ""Configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""unhealthy"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:42""
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""unhealthy"",
         ""Configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:42""
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""running"",
         ""Configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""failed"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:42""
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""LastExitTimeUtc"" : ""0001-01-01T00:00:00.0000000"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""failed"",
         ""Configuration"": {
            ""id"": ""1""
         }
      }
   ],
   ""validJson"":[
      {
         ""Version"":""<semantic_version_number>"",
         ""Type"":""docker"",
         ""Status"":""running"",
         ""RestartPolicy"":""on-failure"",
         ""ImagePullPolicy"": ""on-create"",
         ""Settings"":{
            ""Image"":""image1:42""
         },
         ""ExitCode"": 0,
         ""StatusDescription"" : ""<status description>"",
         ""LastStartTimeUtc"" : ""2017-08-04T17:52:13.0419502Z"",
         ""LastExitTimeUtc"" : ""2017-08-05T17:52:13.0419502Z"",
         ""RestartCount"" : 1,
         ""LastRestartTimeUtc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RuntimeStatus"":""running"",
         ""Configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""version"":""<semantic_version_number>"",
         ""type"":""docker"",
         ""status"":""running"",
         ""restartpolicy"":""on-failure"",
         ""imagepullpolicy"": ""on-create"",
         ""exitcode"": 0,
         ""statusdescription"" : ""<status description>"",
         ""laststarttimeutc"" : ""2017-08-04T17:52:13.0419502Z"",
         ""lastexittimeutc"" : ""2017-08-05T17:52:13.0419502Z"",
         ""settings"":{
            ""image"":""image1:42""
         },
         ""restartcount"" : 1,
         ""lastrestarttimeutc"" : ""2017-08-06T17:52:13.0419502Z"",
         ""runtimestatus"":""running"",
         ""configuration"": {
            ""id"": ""1""
         }
      },
      {
         ""EXITCODE"": 0,
         ""STATUSDESCRIPTION"" : ""<status description>"",
         ""LASTSTARTTIMEUTC"" : ""2017-08-04T17:52:13.0419502Z"",
         ""LASTEXITTIMEUTC"" : ""2017-08-05T17:52:13.0419502Z"",
         ""VERSION"":""<semantic_version_number>"",
         ""TYPE"":""docker"",
         ""STATUS"":""RUNNING"",
         ""RESTARTPOLICY"":""on-failure"",
         ""IMAGEPULLPOLICY"": ""on-create"",
         ""SETTINGS"":{
            ""IMAGE"":""image1:42""
         },
         ""RESTARTCOUNT"" : 1,
         ""LASTRESTARTTIMEUTC"" : ""2017-08-06T17:52:13.0419502Z"",
         ""RUNTIMESTATUS"":""running"",
         ""CONFIGURATION"": {
            ""id"": ""1""
         }
      }
   ],
}
");

        public static IEnumerable<object[]> GetValidJsonInputs()
        {
            return GetJsonTestCases("validJson").Select(s => new object[] { s });
        }

        public static IEnumerable<object[]> GetValidStatusInputs()
        {
            return GetJsonTestCases("validStatus").Select(s => new object[] { s });
        }

        public static IEnumerable<object[]> GetInvalidExitCodes()
        {
            return GetJsonTestCases("invalidExitCodeJson").Select(s => new object[] { s });
        }

        public static IEnumerable<object[]> GetInvalidStatusDescription()
        {
            return GetJsonTestCases("invalidStatusDescription").Select(s => new object[] { s });
        }

        public static IEnumerable<object[]> GetInvalidLastStartTimes()
        {
            return GetJsonTestCases("invalidLastStartTime").Select(s => new object[] { s });
        }

        public static IEnumerable<object[]> GetInvalidLastExitTimes()
        {
            return GetJsonTestCases("invalidLastExitTime").Select(s => new object[] { s });
        }

        public static IEnumerable<object[]> GetInvalidRestartCounts()
        {
            return GetJsonTestCases("invalidRestartCount").Select(s => new object[] { s });
        }

        public static IEnumerable<object[]> GetInvalidLastRestartTimes()
        {
            return GetJsonTestCases("invalidLastRestartTime").Select(s => new object[] { s });
        }

        [Fact]
        public void TestConstructor()
        {
            DateTime lastStartTime = DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind);

            // null docker config
            Assert.Throws<ArgumentNullException>(() => new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, null, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars));

            // bad desired status
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerRuntimeModule("mod1", "version1", (ModuleStatus)int.MaxValue, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars));

            // bad restart policy
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, (RestartPolicy)int.MaxValue, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars));

            // bad runtime status
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, (ModuleStatus)int.MaxValue, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars));

            // bad restart count
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, -1, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars));

            // bad pull policy
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerRuntimeModule("mod1", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, (ImagePullPolicy)int.MaxValue, DefaultConfigurationInfo, EnvVars));

            var env1 = new DockerRuntimeModule("name", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, null, lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var env2 = new DockerRuntimeModule("name", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", DateTime.MinValue, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var env3 = new DockerRuntimeModule("name", "version1", ModuleStatus.Running, RestartPolicy.OnUnhealthy, Config1, 0, "Running 1 minute", lastStartTime, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
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
            Assert.NotEqual(Module8, Module13);

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

            myModule.Name = "mod1";
            moduleFromSerializedModule.Name = "mod1";

            Assert.True(Module2.Equals(myModule));
            Assert.True(moduleFromSerializedModule.Equals(Module2));
        }

        [Fact]
        public void TestSerializeWithNull()
        {
            string jsonFromDockerModule = ModuleSerde.Instance.Serialize(Module1);
            IModule myModule = ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(jsonFromDockerModule);
            IModule moduleFromSerializedModule = ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(SerializedModule1);

            myModule.Name = "mod1";
            moduleFromSerializedModule.Name = "mod1";

            Assert.True(Module1.Equals(myModule));
            Assert.True(moduleFromSerializedModule.Equals(Module1));
        }

        [Theory]
        [MemberData(nameof(GetValidJsonInputs))]
        public void TestDeserializeValidJson(string inputJson)
        {
            var module = ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(inputJson);
            module.Name = "<module_name>";
            Assert.True(ValidJsonModule.Equals(module));
        }

        [Theory]
        [MemberData(nameof(GetValidStatusInputs))]
        public void TestDeserializeValidStatus(string inputJson)
        {
            var module = ModuleSerde.Instance.Deserialize<DockerRuntimeModule>(inputJson);
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
            Assert.Equal(Module1.GetHashCode(), Module1A.GetHashCode());
            Assert.Equal(Module2.GetHashCode(), Module2A.GetHashCode());

            Assert.NotEqual(Module1.GetHashCode(), Module2.GetHashCode());
            Assert.NotEqual(Module1.GetHashCode(), Module8.GetHashCode());
            Assert.NotEqual(Module2.GetHashCode(), Module3.GetHashCode());
            Assert.NotEqual(Module2.GetHashCode(), Module4.GetHashCode());
            Assert.NotEqual(Module2.GetHashCode(), Module5.GetHashCode());
            Assert.NotEqual(Module2.GetHashCode(), Module6.GetHashCode());
            Assert.NotEqual(Module2.GetHashCode(), Module7.GetHashCode());
        }

        [Fact]
        public void TestWithRuntimeStatus()
        {
            var m1 = Module1 as DockerRuntimeModule;
            var newM1 = (DockerRuntimeModule)m1?.WithRuntimeStatus(ModuleStatus.Running);
            var m2 = Module2 as DockerRuntimeModule;
            var newM2 = (DockerRuntimeModule)m2?.WithRuntimeStatus(ModuleStatus.Stopped);

            Assert.Equal(m1, newM1);
            Assert.NotEqual(m2, newM2);
            Assert.NotNull(newM2);
            Assert.Equal(ModuleStatus.Stopped, newM2.RuntimeStatus);
            Assert.Equal(m2.Config, newM2.Config);
            Assert.Equal(m2.ConfigurationInfo, newM2.ConfigurationInfo);
            Assert.Equal(m2.DesiredStatus, newM2.DesiredStatus);
            Assert.Equal(m2.ExitCode, newM2.ExitCode);
            Assert.Equal(m2.LastExitTimeUtc, newM2.LastExitTimeUtc);
            Assert.Equal(m2.LastRestartTimeUtc, newM2.LastRestartTimeUtc);
            Assert.Equal(m2.LastStartTimeUtc, newM2.LastStartTimeUtc);
            Assert.Equal(m2.Name, newM2.Name);
            Assert.Equal(m2.RestartCount, newM2.RestartCount);
            Assert.Equal(m2.RestartPolicy, newM2.RestartPolicy);
            Assert.Equal(m2.ImagePullPolicy, newM2.ImagePullPolicy);
            Assert.Equal(m2.StatusDescription, newM2.StatusDescription);
            Assert.Equal(m2.Type, newM2.Type);
            Assert.Equal(m2.Version, newM2.Version);
        }

        static IEnumerable<string> GetJsonTestCases(string subset)
        {
            var val = (JArray)TestJsonInputs.GetValue(subset);
            return val.Children().Select(token => token.ToString());
        }
    }
}
