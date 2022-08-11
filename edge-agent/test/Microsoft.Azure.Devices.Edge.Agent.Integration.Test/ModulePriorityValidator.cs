// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Integration.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
    using Newtonsoft.Json;
    using Xunit;

    public class ModulePriorityValidator : Validator
    {
        [JsonProperty(PropertyName = "moduleCommands")]
        public ModuleCommand[] ModuleCommands { get; set; }

        public override bool Validate(TestCommandFactory testCommandFactory)
        {
            var recorded = testCommandFactory.Recorder.Expect(() => new Exception("Expected test command factory to have recorder.")).ExecutionList;

            Assert.Equal(this.ModuleCommands.Length, recorded.Count);
            for (int i = 0; i < recorded.Count; i++)
            {
                Assert.Equal(this.ModuleCommands[i].Command, recorded[i].TestType.ToString());
                Assert.Equal(this.ModuleCommands[i].ModuleName, recorded[i].Module.Name);
            }

            return true;
        }
    }

    public class ModuleCommand
    {
        [JsonProperty(PropertyName = "command")]
        public string Command { get; set; }

        [JsonProperty(PropertyName = "moduleName")]
        public string ModuleName { get; set; }
    }
}
