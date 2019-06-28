// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;

    [Serializable]
    public class InvalidKubernetesNameException : Exception
    {
        public InvalidKubernetesNameException(string message) : base(message)
        {

        }
        public InvalidKubernetesNameException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}

