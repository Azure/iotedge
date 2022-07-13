// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Integration.Test
{
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;

    public abstract class Validator
    {
        public abstract bool Validate(TestCommandFactory testCommandFactory);
    }
}
