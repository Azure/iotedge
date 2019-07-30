// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;

    [Serializable]
    public class InvalidModuleException : Exception
    {
        public InvalidModuleException(string message)
            : base(message)
        {
        }

        public InvalidModuleException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
