// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Integration.Test
{
    using Newtonsoft.Json;
    using Xunit;

    public class ModulePriorityValidator : Validator
    {
        [JsonProperty(PropertyName = "moduleCommands")]
        public ModuleCommand[] ModuleCommands { get; set; }

        public override bool Validate(TestCommandFactory testCommandFactory)
        {
            var recorded = testCommandFactory.RecordedCommands;
            for (int i = 0; i < recorded.Count; i++)
            {
                Assert.Equal(this.ModuleCommands[i].Command, recorded[i].Item1);
                Assert.Equal(this.ModuleCommands[i].ModuleName, recorded[i].Item2);
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
