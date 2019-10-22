// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;

    /// <summary>
    /// Utility functions supporting the backup and restore of storage data.
    /// </summary>
    public static class BackupRestoreUtil
    {
        public static SerializationFormat GetFormat(IDataBackupRestore backupRestore)
        {
            if (typeof(ProtoBufDataBackupRestore).IsInstanceOfType(backupRestore))
            {
                return SerializationFormat.ProtoBuf;
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
