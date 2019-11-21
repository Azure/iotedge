// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class TestAgentInformation
    {
        public string ConsumerGroupId { get; }
        public string DeviceId { get; }
        public string Os { get; }
        public string OsArchitecture { get; }
        public string OsDescription { get; }
        public string OsVersion { get; }
        public string ProcessArchitecture { get; }
        public string TimeTicks { get; }
        public string User { get; }
        public string Version { get; }
        public string VstsAgentMachineName { get; }
        public string VstsAgentName { get; }
        public string VstsBuildId { get; }
        public string VstsBuildNumber { get; }
        public string VstsBuildQueueBy { get; }

        public TestAgentInformation()
        {
            string[] osDescriptionTokens = System.Runtime.InteropServices.RuntimeInformation.OSDescription.Split(' ');

            this.ConsumerGroupId = Settings.Current.ConsumerGroupId;
            this.DeviceId = Settings.Current.DeviceId;
            this.Os = osDescriptionTokens[0];
            this.OsArchitecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
            this.OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            this.OsVersion = osDescriptionTokens[1];
            this.ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
            this.TimeTicks = DateTime.UtcNow.Ticks.ToString();
            this.User = Environment.UserName.ToString();
            this.Version = "1";
            this.VstsAgentMachineName = Settings.Current.VstsAgentMachineName;
            this.VstsAgentName = Settings.Current.VstsAgentName;
            this.VstsBuildId = Settings.Current.VstsBuildId;
            this.VstsBuildNumber = Settings.Current.VstsBuildNumber;
            this.VstsBuildQueueBy = Settings.Current.VstsBuildQueueBy;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}