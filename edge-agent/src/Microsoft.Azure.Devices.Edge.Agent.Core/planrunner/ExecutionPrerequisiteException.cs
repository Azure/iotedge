// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunner
{
    using System;

    public class ExecutionPrerequisiteException : Exception
    {
        public ExecutionPrerequisiteException()
        {
        }

        public ExecutionPrerequisiteException(string message)
            : base(message)
        {
        }

        public ExecutionPrerequisiteException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
