// Copyright (c) Microsoft. All rights reserved.
namespace Analyzer
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
        public string EdgeletBuildId { get; }
        public string EdgeletBuildBranch { get; }
        public string ModuleImageBuildId { get; }
        public string ModuleImageBuildBranch { get; }
        public string Os { get; }
        public string OsArchitecture { get; }
        public string OsDescription { get; }
        public string OsVersion { get; }
        public string ProcessArchitecture { get; }
        public string User { get; }

        public TestAgentInformation()
        {
            string[] OsDescriptionTokens = System.Runtime.InteropServices.RuntimeInformation.OSDescription.Split(' ');

            this.ConsumerGroupId = Settings.Current.ConsumerGroupId;
            this.DeviceId = Settings.Current.DeviceId;
            this.EdgeletBuildId = Settings.Current.EdgeletBuildId;
            this.EdgeletBuildBranch = Settings.Current.EdgeletBuildBranch;
            this.ModuleImageBuildId = Settings.Current.ModuleImageBuildId;
            this.ModuleImageBuildBranch = Settings.Current.ModuleImageBuildBranch;
            this.Os = OsDescriptionTokens[0];
            this.OsArchitecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
            this.OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            this.OsVersion = OsDescriptionTokens[1];
            this.ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
            this.User = Environment.UserName.ToString();
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}