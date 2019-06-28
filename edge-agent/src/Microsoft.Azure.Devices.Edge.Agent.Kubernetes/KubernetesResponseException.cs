// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;

    [Serializable]
    public class KuberenetesResponseException : Exception
    {
        public KuberenetesResponseException(string message) : base(message)
        {

        }
        public KuberenetesResponseException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}
