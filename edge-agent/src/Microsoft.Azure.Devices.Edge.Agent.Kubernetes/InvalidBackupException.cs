// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;

    [Serializable]
    public class InvalidBackupException : Exception
    {
        public InvalidBackupException(string message)
            : base(message)
        {
        }

        public InvalidBackupException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
