// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;

    public static class BackupRestoreUtil
    {
        public static SerializationFormat GetFormat(IBackupRestore backupRestore)
        {
            if (typeof(ProtoBufBackupRestore).IsInstanceOfType(backupRestore))
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
