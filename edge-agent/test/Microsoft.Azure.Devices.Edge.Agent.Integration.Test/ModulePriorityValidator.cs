// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Integration.Test
{
    using Xunit;

    public class ModulePriorityValidator : Validator
    {
        public ModuleCommand[] moduleCommands;

        public override bool Validate(TestCommandFactory testCommandFactory)
        {
            var recorded = testCommandFactory.RecordedCommands;
            for (int i = 0; i < recorded.Count; i++)
            {
                Assert.Equal(moduleCommands[i].Command, recorded[i].Item1);
                Assert.Equal(moduleCommands[i].ModuleName, recorded[i].Item2);
            }

            return true;
        }
    }

    public class ModuleCommand
    {
        public string Command { get; set; }

        public string ModuleName { get; set; }
    }
}
