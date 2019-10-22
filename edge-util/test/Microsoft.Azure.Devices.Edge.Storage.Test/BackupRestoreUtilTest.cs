// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class BackupRestoreUtilTest
    {
        class TestBackupRestore : IDataBackupRestore
        {
            Task IDataBackupRestore.BackupAsync<T>(string name, string backupPath, T data)
            {
                throw new NotImplementedException();
            }

            Task<T> IDataBackupRestore.RestoreAsync<T>(string name, string backupPath)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void GetFormatTest()
        {
            IDataBackupRestore backupRestore = new TestBackupRestore();
            Assert.Throws<NotSupportedException>(() => BackupRestoreUtil.GetFormat(backupRestore));

            backupRestore = new ProtoBufDataBackupRestore();
            Assert.Equal(SerializationFormat.ProtoBuf, BackupRestoreUtil.GetFormat(backupRestore));
        }
    }
}
