// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.E2E.Test
{
    public enum ValidatorType
    {
        RunCommand
    }

    public abstract class Validator
    {
        public ValidatorType Type { get; set; }

        public abstract bool Validate();
    }
}
