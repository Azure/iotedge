// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;

    public static class BackupRestoreFactory
    {
        public static IBackupRestore GetInstance(SerializationFormat format)
        {
            switch (format)
            {
                case SerializationFormat.ProtoBuf:
                    return new ProtoBufBackupRestore();
                default:
                    throw new NotSupportedException();
            }
        }

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
