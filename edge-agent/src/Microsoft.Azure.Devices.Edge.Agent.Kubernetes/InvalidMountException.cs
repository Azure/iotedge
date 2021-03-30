// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;

    [Serializable]
    public class InvalidMountException : Exception
    {
        public InvalidMountException(string message)
            : base(message)
        {
        }

        public InvalidMountException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
