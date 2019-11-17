// Copyright (c) Microsoft. All rights reserved.
namespace Analyzer
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class TestAgentInformation
    {
        public string Architecture { get; }
        public string ConsumerGroupId { get; }
        public string Name { get; }
        public string Os { get; }
        public string OsVersion { get; }
        public string User { get; }
        public string DeviceId { get; }

        public TestAgentInformation()
        {
            this.Architecture = Architecture.ToString();
            this.ConsumerGroupId = Settings.Current.ConsumerGroupId;
            this.Name = Settings.Current.DeviceId;
            this.Os = OSPlatform.ToString();
            this.OsVersion = RuntimeEnvironment.GetSystemVersion();
            this.User = agentUser;
            this.DeviceId = Settings.Current.DeviceId;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}