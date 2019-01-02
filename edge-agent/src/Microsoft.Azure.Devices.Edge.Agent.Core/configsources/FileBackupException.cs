// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;

    [Serializable]
    public class FileBackupException : Exception
    {
        public FileBackupException(string message) : base(message)
        {
        }

        public FileBackupException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
