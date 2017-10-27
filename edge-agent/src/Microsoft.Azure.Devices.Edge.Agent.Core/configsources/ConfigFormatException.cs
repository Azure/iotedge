// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;

    [Serializable]
    public class ConfigFormatException : Exception
    {
        public ConfigFormatException(string message) : base(message)
        {
        }

        public ConfigFormatException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
