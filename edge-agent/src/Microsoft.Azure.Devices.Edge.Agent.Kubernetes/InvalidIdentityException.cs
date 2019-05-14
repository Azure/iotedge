// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;

    [Serializable]
    public class InvalidIdentityException : Exception
    {
        public InvalidIdentityException(string message) : base(message)
        {

        }
        public InvalidIdentityException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}

