// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Integration.Test
{
    public abstract class Validator
    {
        public abstract bool Validate(TestCommandFactory testCommandFactory);
    }
}
