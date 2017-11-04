// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;

    [Serializable]
    public class ConfigEmptyException : Exception
    {
        public ConfigEmptyException(string message) : base(message)
        {
        }

        public ConfigEmptyException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
