// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunner
{
    using System;

    public class ExcecutionPrerequisiteException : Exception
    {
        public ExcecutionPrerequisiteException()
        {
        }

        public ExcecutionPrerequisiteException(string message)
            : base(message)
        {
        }

        public ExcecutionPrerequisiteException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
