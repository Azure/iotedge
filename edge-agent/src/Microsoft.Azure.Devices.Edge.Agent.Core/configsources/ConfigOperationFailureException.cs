// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;

    [Serializable]
    public class ConfigOperationFailureException : Exception
    {
        public ConfigOperationFailureException(string message)
            : base(message)
        {
        }

        public ConfigOperationFailureException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
