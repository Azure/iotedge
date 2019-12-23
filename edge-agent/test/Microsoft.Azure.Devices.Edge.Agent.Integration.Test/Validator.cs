using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Devices.Edge.Agent.Integration.Test
{
    public enum ValidatorType
    {
        ModulePriority
    }

    public abstract class Validator
    {
        public ValidatorType Type { get; set; }

        public abstract bool Validate(TestCommandFactory testCommandFactory);
    }
}
